using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using FileProcessor.Providers;
using Org.BouncyCastle.Bcpg;
using Org.BouncyCastle.Bcpg.OpenPgp;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.Utilities.IO;

namespace FileProcessor.Transformers
{
    public class PgpTransformer : IAsyncStep<Stream, IFileReference>, IDisposable
    {
        private readonly PgpTransformerOptions options;
        private string tmp;

        public PgpTransformer(PgpTransformerOptions options)
        {
            if (!options.IsValid(out var message)) throw new ArgumentException(message, nameof(options));
            this.options = options;
        }

        public async Task<IFileReference> Execute(Stream input)
        {
            this.tmp = Path.GetTempFileName();
            await using (var output = File.OpenWrite(this.tmp))
            {
                if (this.options.Mode == PgpTransformerMode.Encrypt)
                {
                    var key = getKey(this.options.PublicKey);
                    await encrypt(key, input, output, this.options.Armor, this.options.TestIntegrity);
                }

                if (this.options.Mode == PgpTransformerMode.Decrypt)
                {
                    await using var key = new MemoryStream(this.options.PrivateKey);
                    await decrypt(input, output, key, this.options.Password, this.options.TestIntegrity);
                }
            }

            return new LocalFile(this.tmp);
        }

        public void Dispose()
        {
            if (this.tmp != null)
                //todo: secure erase?
                File.Delete(this.tmp);
        }

        private static async Task encrypt(PgpPublicKey key, Stream inputStream, Stream outputStream, bool armor,
            bool integrityCheck)
        {
            var tmpCompressed = Path.GetTempFileName(); //todo: delete
            try
            {
                if (armor) outputStream = new ArmoredOutputStream(outputStream);

                await using var compressed = File.Open(tmpCompressed, FileMode.Create, FileAccess.ReadWrite);

                var dataCompressor = new PgpCompressedDataGenerator(CompressionAlgorithmTag.Zip);
                var compressor = dataCompressor.Open(compressed);
                var lData = new PgpLiteralDataGenerator();
                await using var compressorInput = lData.Open(compressor, PgpLiteralData.Binary, "encrypted_data.dat",
                    inputStream.Length, DateTime.UtcNow);
                await inputStream.CopyToAsync(compressorInput);
                compressorInput.Close();
                dataCompressor.Close();

                var dataGenerator = new PgpEncryptedDataGenerator(SymmetricKeyAlgorithmTag.Cast5,
                    integrityCheck, new SecureRandom());
                dataGenerator.AddMethod(key);

                compressed.Position = 0;
                await using var encStream = dataGenerator.Open(outputStream, compressed.Length);
                await compressed.CopyToAsync(encStream);
                encStream.Close();

                if (armor) outputStream.Close();
            }
            finally
            {
                File.Delete(tmpCompressed);
            }
        }

        private static async Task decrypt(Stream input, Stream output, Stream key, string password,
            bool testIntegrity = true)
        {
            input = PgpUtilities.GetDecoderStream(input);
            var pgpF = new PgpObjectFactory(input);
            PgpEncryptedDataList enc;

            var o = pgpF.NextPgpObject();
            if (o is PgpEncryptedDataList list)
                enc = list;
            else
                enc = (PgpEncryptedDataList) pgpF.NextPgpObject();
            PgpPrivateKey sKey = null;
            PgpPublicKeyEncryptedData pbe = null;
            var pgpSec = new PgpSecretKeyRingBundle(
                PgpUtilities.GetDecoderStream(key));

            foreach (PgpPublicKeyEncryptedData encData in enc.GetEncryptedDataObjects())
            {
                sKey = findSecretKey(pgpSec, encData.KeyId, password.ToCharArray());

                if (sKey != null)
                {
                    pbe = encData;
                    break;
                }
            }

            if (sKey == null) throw new ArgumentException("secret key for message not found.");


            var clear = pbe.GetDataStream(sKey);

            var plainFact = new PgpObjectFactory(clear);

            var message = plainFact.NextPgpObject();

            if (message is PgpCompressedData cData)
            {
                var pgpFact = new PgpObjectFactory(cData.GetDataStream());
                message = pgpFact.NextPgpObject();
            }

            switch (message)
            {
                case PgpLiteralData ld:
                {
                    await using var unc = ld.GetInputStream();
                    Streams.PipeAll(unc, output);
                    break;
                }
                case PgpOnePassSignatureList _:
                    throw new PgpException("encrypted message contains a signed message - not literal data.");
                default:
                    throw new PgpException("message is not a simple encrypted file - type unknown.");
            }

            if (testIntegrity && (!pbe.IsIntegrityProtected() || !pbe.Verify()))
                throw new PgpException("integrity check failed");
        }

        private static PgpPrivateKey findSecretKey(PgpSecretKeyRingBundle pgpSec, long keyId, char[] pass)
        {
            var pgpSecKey = pgpSec.GetSecretKey(keyId);

            return pgpSecKey?.ExtractPrivateKey(pass);
        }

        private static PgpPublicKey getKey(byte[] keyBytes)
        {
            using var ms = new MemoryStream(keyBytes);
            var bundle = new PgpPublicKeyRingBundle(
                PgpUtilities.GetDecoderStream(ms));
            foreach (PgpPublicKeyRing keyRing in bundle.GetKeyRings())
            foreach (PgpPublicKey key in keyRing.GetPublicKeys())
                if (key.IsEncryptionKey)
                    return key;

            throw new ArgumentException("Can't find encryption key in key ring.");
        }
    }

    public class PgpTransformerOptions
    {
        public string PublicKeyText
        {
            get => Encoding.ASCII.GetString(this.PublicKey);
            set => this.PublicKey = Encoding.ASCII.GetBytes(value ?? string.Empty);
        }

        public string PrivateKeyText
        {
            get => Encoding.ASCII.GetString(this.PrivateKey);
            set => this.PrivateKey = Encoding.ASCII.GetBytes(value ?? string.Empty);
        }

        public byte[] PublicKey { get; set; } = new byte[0];
        public PgpTransformerMode Mode { get; set; }
        public byte[] PrivateKey { get; set; } = new byte[0];
        public bool TestIntegrity { get; set; } = true;
        public string Password { get; set; }
        public bool Armor { get; set; } = true;

        public bool IsValid(out string error)
        {
            error = string.Empty;
            if (this.Mode == PgpTransformerMode.Encrypt && (this.PublicKey.Length == 0 || this.PrivateKey.Length != 0))
                error = "If encrypting public key should be set and private should not";
            else if (this.Mode == PgpTransformerMode.Decrypt &&
                     (this.PublicKey.Length != 0 || this.PrivateKey.Length == 0 || string.IsNullOrEmpty(this.Password)))
                error = "If decrypting private key should be set and public should not";

            return error == string.Empty;
        }
    }

    public enum PgpTransformerMode
    {
        Encrypt,
        Decrypt
    }
}