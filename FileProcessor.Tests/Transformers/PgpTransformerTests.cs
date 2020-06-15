using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using FileProcessor.Transformers;
using Org.BouncyCastle.Bcpg;
using Org.BouncyCastle.Bcpg.OpenPgp;
using Org.BouncyCastle.Security;
using Xunit;

namespace FileProcessor.Tests.Transformers
{
    public class PgpTransformerTests
    {
        private const string PublicKey = "-----BEGIN PGP PUBLIC KEY BLOCK-----\n" +
                                         "Version: Keybase OpenPGP v1.0.0\n" +
                                         "Comment: https://keybase.io/crypto\n" +
                                         "\n" +
                                         "xo0EXuagoAEEAK5lMUh+JmtlyuWY7y0gDc6NHG5SQp7qbGoY2LF924F9iOCGBFqh\n" +
                                         "fLWPNpVSum+hKEsl0ljkFR6qeEVO/1hSLc0D+365m6hRoWVJZ2C1FJSMAFAlyTo7\n" +
                                         "2LhE4rmZ73JM/WzFjuhJr0qcQpOHEZp6V421cRmbAQReCXMJpaGOkp+dABEBAAHN\n" +
                                         "FHRlc3QgPHRlc3RAdGVzdC5jb20+wq0EEwEKABcFAl7moKACGy8DCwkHAxUKCAIe\n" +
                                         "AQIXgAAKCRCcVaB+Uhy8OWaoA/0QgvWlsJpS+6fdYp10Wua2Z3zdqJc0BiUpoqIz\n" +
                                         "8Jg/9HO0ioSvso+pddtSgrpRlCd2Wb10hnydZlHG39OjkMqW4H2UVfNRaVSRyFAy\n" +
                                         "18cwJqj2WAzqg9LxVBJbFfXcRUhcXCSRBU2qYiwTS/K0N575NDxe5qKyPFoStKAq\n" +
                                         "iZyt186NBF7moKABBADS6NlxwhY0+/V0RQsZmli6kyH3ujs/78DSKLUBbgelqgVE\n" +
                                         "csST4rtPuS5gq9cu0YCsTYDzsTIrBPxgF8rjniKhUgpHI38d9dlzUVx7ijotKi7v\n" +
                                         "PDvripaOr0S6jbfblvKLCZq6In6oLBo0toQP5oRS2y1L64aYfrzTUW5WewQ0UQAR\n" +
                                         "AQABwsCDBBgBCgAPBQJe5qCgBQkPCZwAAhsuAKgJEJxVoH5SHLw5nSAEGQEKAAYF\n" +
                                         "Al7moKAACgkQf/GJ2rbP/0qlIwQAoamFGcdOw0580wTvZ/dHOSvoo1bZkTbNiDpZ\n" +
                                         "QZ26nRUYbOsWpeCjv8eavloX/tMV0vpQwu7BN9aLTPIoUUmr+Ej5yXBtbiXY0/wE\n" +
                                         "65a+RIZ36n48Ptl6Up2LTxJylQoHp4nyLlQS8EH2nxxdts2wiJQ3VfZAXPdID+CM\n" +
                                         "05Ml8TZKHQP9EHUGOqGZkZUmWDSdaHZrYjwxe1r3FiCufCS/N6sRq7DpMili6Jmz\n" +
                                         "sawAJIcugm87o9q9OQIMMGerkpcMiN3lACYjJ6cUDDYsWSYGASY5llZRUZj6gaBQ\n" +
                                         "gs9498AjpfICgZAD2ONpe/46+oz2/QJ2tAxLB+OAGTcIIdXbeP1uzbTOjQRe5qCg\n" +
                                         "AQQAooGMrgEBqugcLPXwQS8Nb1ING/Zj/0mgvniAyfQ4T6oJOsAu0qIUNgcz0+id\n" +
                                         "sK7ksvSYiYPBElIWJCImHhdZ9ItDQ2Lh44giKC6goat5LtAvGWLP5fPqakJOycS3\n" +
                                         "NewEsMi11vUbTwt784zX42Wq71wd212EomF8nm7t9m0komEAEQEAAcLAgwQYAQoA\n" +
                                         "DwUCXuagoAUJDwmcAAIbLgCoCRCcVaB+Uhy8OZ0gBBkBCgAGBQJe5qCgAAoJEIVw\n" +
                                         "JH2QVbqfmyED/RydmUXBbmB3VySI7GRPnlE/3layrBlffqHFrke73Vntp87kcNZR\n" +
                                         "RzVcbGgBbAvZcdU53mDTMi00U6hVaYVUWNyejJ6QNOKkiAcPiSeGkTt4hw0rMItb\n" +
                                         "n37CV3IM6rZ5gnLdad/mnNdXL0AJlkFdjmqwrlx2DYBpFbI1kVjWKJXo+9QEAJq5\n" +
                                         "nrLkVwexoEFipu/CfXsA8R81l+oqm0hiCXKo/EWps9OUkgEiDnSN8pjw+SR5np5U\n" +
                                         "fIvuL1a69yQoEh5xsAM4hHCXvOsvV6MD8F5xTv9sEhL2l/DCWM0ylyQknTI0RN9I\n" +
                                         "XCi4zbqWqjdmiMrF5Sk541HA9P0BItEwTK9WIgaR\n" +
                                         "=pNd2\n" +
                                         "-----END PGP PUBLIC KEY BLOCK-----\n";

        private const string Password = "testing";

        private const string PrivateKey = "-----BEGIN PGP PRIVATE KEY BLOCK-----\n" +
                                          "Version: Keybase OpenPGP v1.0.0\n" +
                                          "Comment: https://keybase.io/crypto\n" +
                                          "\n" +
                                          "xcFGBF7moKABBACuZTFIfiZrZcrlmO8tIA3OjRxuUkKe6mxqGNixfduBfYjghgRa\n" +
                                          "oXy1jzaVUrpvoShLJdJY5BUeqnhFTv9YUi3NA/t+uZuoUaFlSWdgtRSUjABQJck6\n" +
                                          "O9i4ROK5me9yTP1sxY7oSa9KnEKThxGaeleNtXEZmwEEXglzCaWhjpKfnQARAQAB\n" +
                                          "/gkDCFPjDDH5VSx8YPbK5VLMeNpkGSEUgLg0Do7XhPhqZ41AUzpftwOCr4es09WZ\n" +
                                          "SHqUlw25HfKygHr3c+Wn/wlRqH/ToAQb+SrJiWINbFGfwW0vNLTGE6DC0OTSNdpK\n" +
                                          "u0dtGsUFaLnDc/WCsb2zcz/yTdmHL1VqTif/LGbZ2d1ej3UVQuyqtU4aaMlVVpEU\n" +
                                          "Kq6vdjAYiG5lcxlY9+Q3KuRen5DKaWMY8wVzBr08HpGf+63LGRvRufqePzHoxQZz\n" +
                                          "HKrLbFA1XdEciVOGBvm5NuzIC7hjMfeP7CqzqGmX+/IZIoh4299NSBAo5512SgDl\n" +
                                          "QMu2W+PRPvseniZN3t6nfpsZccGsKEMW1ubKkIOrY8orm6eF8qxBuwPncDdO7nTx\n" +
                                          "Zg/9scBTgTfv3m0yJniG5PEEGnTaUOmMWehQfvbq96UTMeE4lVGqICK4BSZy703J\n" +
                                          "Vm5pl/swSaMqkLnwvT9HYUIi76thhADRyDkyenGb7v1iLvW+byfsKrvNFHRlc3Qg\n" +
                                          "PHRlc3RAdGVzdC5jb20+wq0EEwEKABcFAl7moKACGy8DCwkHAxUKCAIeAQIXgAAK\n" +
                                          "CRCcVaB+Uhy8OWaoA/0QgvWlsJpS+6fdYp10Wua2Z3zdqJc0BiUpoqIz8Jg/9HO0\n" +
                                          "ioSvso+pddtSgrpRlCd2Wb10hnydZlHG39OjkMqW4H2UVfNRaVSRyFAy18cwJqj2\n" +
                                          "WAzqg9LxVBJbFfXcRUhcXCSRBU2qYiwTS/K0N575NDxe5qKyPFoStKAqiZyt18fB\n" +
                                          "RgRe5qCgAQQA0ujZccIWNPv1dEULGZpYupMh97o7P+/A0ii1AW4HpaoFRHLEk+K7\n" +
                                          "T7kuYKvXLtGArE2A87EyKwT8YBfK454ioVIKRyN/HfXZc1Fce4o6LSou7zw764qW\n" +
                                          "jq9Euo2325byiwmauiJ+qCwaNLaED+aEUtstS+uGmH6801FuVnsENFEAEQEAAf4J\n" +
                                          "AwiEx5yf5zKsTWBMmUAqSSQ9QMtcuabvrxqSmpnTSkZSHSfhm1L83oE7Hpd/OXC1\n" +
                                          "LSmbWZjTBgZHpIA2sjYkdRQ8nNLZtGM/rog5FwIoFkLjVkUsaSGxBTNreSZdvCGm\n" +
                                          "szle9xS33KTLv60BcLyrGkW+ZSv4rtLYyQ6Ar8yTmgqH6SQaCzHGKM5smIvlLjRh\n" +
                                          "psQim2Mlx7DnTUnvkl+Tzxe49dkSJA+R7i18c1kx5Ps6Jma78ZyFApG6xGI0srsW\n" +
                                          "QcTbREvuqJh3QT7xOhUqZXDprsM9opMcjWEG8RlLmAI/jyobYMhHAmhxjBgRQp6Y\n" +
                                          "+FL1OdSdMXSHUc/ZCX266uk+aKtIgT/3jxY7d0DT04BQK42AlAzzX4R/NlUzSLbF\n" +
                                          "Q8XrAjBy85ueu0bYfHi6ZYqOxvrNWOf3jPAQQqOzAqDc8gpMCA4w/RGRJlldnqe0\n" +
                                          "drbhkIUaWtApSBs29QY15w4FBUSkYjf5fKzkoVacw2JodI5KxG1MwsCDBBgBCgAP\n" +
                                          "BQJe5qCgBQkPCZwAAhsuAKgJEJxVoH5SHLw5nSAEGQEKAAYFAl7moKAACgkQf/GJ\n" +
                                          "2rbP/0qlIwQAoamFGcdOw0580wTvZ/dHOSvoo1bZkTbNiDpZQZ26nRUYbOsWpeCj\n" +
                                          "v8eavloX/tMV0vpQwu7BN9aLTPIoUUmr+Ej5yXBtbiXY0/wE65a+RIZ36n48Ptl6\n" +
                                          "Up2LTxJylQoHp4nyLlQS8EH2nxxdts2wiJQ3VfZAXPdID+CM05Ml8TZKHQP9EHUG\n" +
                                          "OqGZkZUmWDSdaHZrYjwxe1r3FiCufCS/N6sRq7DpMili6JmzsawAJIcugm87o9q9\n" +
                                          "OQIMMGerkpcMiN3lACYjJ6cUDDYsWSYGASY5llZRUZj6gaBQgs9498AjpfICgZAD\n" +
                                          "2ONpe/46+oz2/QJ2tAxLB+OAGTcIIdXbeP1uzbTHwUYEXuagoAEEAKKBjK4BAaro\n" +
                                          "HCz18EEvDW9SDRv2Y/9JoL54gMn0OE+qCTrALtKiFDYHM9PonbCu5LL0mImDwRJS\n" +
                                          "FiQiJh4XWfSLQ0Ni4eOIIiguoKGreS7QLxliz+Xz6mpCTsnEtzXsBLDItdb1G08L\n" +
                                          "e/OM1+Nlqu9cHdtdhKJhfJ5u7fZtJKJhABEBAAH+CQMI03L9pVuQ8Adgr1OUTQWl\n" +
                                          "Y1P3mvuql+uMtPNhMuujzXGQ6yJ+oRCJ9ZJwMAxRy31PkMfptH9eUMQWg7SMBxAu\n" +
                                          "UZ2O4dAkDZ8zPKldxzcLhbA5U0sgXJsh1cD//AVndh1q6JV8F0oXX8faMwLp9hey\n" +
                                          "97I1e25GjdR/13yZBchuaUF8KHSr/r96JJIMBJW+lKX3NyJrssk+t9v5FuwpXhKl\n" +
                                          "ji+2y9RpwrfPXAG+3m9zGNkb5fDdvtDN3AevlHQ6LMGvVaCqF6cachOXCdxxaax7\n" +
                                          "EO9o8sk8DIEgw4kUoh0d0MeuNSBXYKAh0JjiYUSi7jOrXo8ryiYqtu4Ua3TrfpRz\n" +
                                          "nbLyxEOJi8EaU9uaqQTs/9ufh5W6rh0ptxsBFbqnRqfQXDGu9OZUiwoFNMS0CnXA\n" +
                                          "twv+V6DMgYXrzmj70P8V01yL5KpizB/Y8/RqNs0dr6/ETitlbk/JTZT72RvEyyY0\n" +
                                          "Ky9g2EChYuSs0rf7ip78ax6VKLhZAMLAgwQYAQoADwUCXuagoAUJDwmcAAIbLgCo\n" +
                                          "CRCcVaB+Uhy8OZ0gBBkBCgAGBQJe5qCgAAoJEIVwJH2QVbqfmyED/RydmUXBbmB3\n" +
                                          "VySI7GRPnlE/3layrBlffqHFrke73Vntp87kcNZRRzVcbGgBbAvZcdU53mDTMi00\n" +
                                          "U6hVaYVUWNyejJ6QNOKkiAcPiSeGkTt4hw0rMItbn37CV3IM6rZ5gnLdad/mnNdX\n" +
                                          "L0AJlkFdjmqwrlx2DYBpFbI1kVjWKJXo+9QEAJq5nrLkVwexoEFipu/CfXsA8R81\n" +
                                          "l+oqm0hiCXKo/EWps9OUkgEiDnSN8pjw+SR5np5UfIvuL1a69yQoEh5xsAM4hHCX\n" +
                                          "vOsvV6MD8F5xTv9sEhL2l/DCWM0ylyQknTI0RN9IXCi4zbqWqjdmiMrF5Sk541HA\n" +
                                          "9P0BItEwTK9WIgaR\n" +
                                          "=eQfi\n" +
                                          "-----END PGP PRIVATE KEY BLOCK-----\n";

        private const string PlainText =
            "This isn't really sensitive - it's just a test. But if it makes it more interesting then we can pretend it is something secret like launch codes!\n" +
            "LaunchCode: 123567covfefe";

        //private const string CipherText =
        //    "-----BEGIN PGP MESSAGE-----\n" +
        //    "Version: BCPG C# v" +
        //    "\n\n" +
        //    "hIwDnFWgflIcvDkBA/94oPYMhfCNOYMX5NBx2UdRg5LPk6jdTtruQmj5/tqdLvFG\n" +
        //    "p/hsf6Knx+PrSqHEQ6uLWuiY1ARhNfkot18HEKiwT7Ymhk+gPtt07zn4OjYBe7M5\n" +
        //    "SlATogaijv9QmzndGhRiAy1v2YobQSnObwUNCM/LSf4VK6lmGJfbbvDeD8UmatK3\n" +
        //    "AcNdb73Gu0It7x++JVbrw8l2KxbWfKt1iM7KTWo8MQZwA+XhB39To1TUN2eBGIdT\n" +
        //    "keDTrVok/SVOBYmAWm/CuIlsJeP+uRcDUbXY2jHi+tBHzgoJolcyKiLOolsaPdX4\n" +
        //    "RZkoJgQNVU99pdT6i/TG5lHoxrQ6CeSQrJx+Zx6NfjzcAVSs/p3WutCaYG+X0McP\n" +
        //    "P9akwSSlGlbfQbQCnQzCIUxM5ZpsW9kO8Lzebye1iLXYtQl1Jexu\n" +
        //    "=oBZX\n" +
        //    "-----END PGP MESSAGE-----\n";

        private const string CipherText =
            "-----BEGIN PGP MESSAGE-----\n" +
            "Version: OpenPGP v2.0.8\n" +
            "Comment: https://sela.io/pgp/\n" +
            "\n" +
            "wYwDf/GJ2rbP/0oBBACFFZ/CDg7eBUdAAnLoubgSBa3lbOApEDYHVVPLA/aWP6DJ\n" +
            "UVgFCZlgYTEUH8giPE1DWUnFqpx8FnNWnYIyhG2xfGmi2A3/KbVXA3jyNSLXQK78\n" +
            "zIvf+dQSsanZq/1YBrg6vvY1D2mr8TE6a5BSjoX5vzVmjbrAeaZn2TqFtUPGWNLA\n" +
            "BQF3LfMSxcwm9oB5c1jMkQahy22Kifas+IEN+OU3ymoKgEwckdU40WLgmLRldmzf\n" +
            "qVEf9Zv3weHY4tyv4VzO3QKe+5F84H781EjG2BGofDJ02zJOSQSZ9iKGKR5aJC7n\n" +
            "J9dJUyFdHFNZ/Plpwv8VgqW99G+ZS38u1yOB1irAAURoDFz/RsBN7jgxJQp9hUVO\n" +
            "c6LV+3dPbbZ9VopP6tfXKKcWrBxh0JFbp+kt868jH2+L2xe6jgn+AKLocIXoVinI\n" +
            "akODLLh3\n" +
            "=dKFi\n" +
            "-----END PGP MESSAGE-----\n";

        [Fact]
        public async Task ExecuteEncryptsData()
        {
            var transformer = new PgpTransformer(new PgpTransformerOptions
            {
                PublicKeyText = PublicKey,
                Mode = PgpTransformerMode.Encrypt
            });

            var inStream = new MemoryStream(Encoding.ASCII.GetBytes(PlainText));
            var result = await transformer.Execute(inStream);
            using var reader = new StreamReader(result);
            string output = await reader.ReadToEndAsync();
            Assert.NotEmpty(output);

        }


        [Fact]
        public async Task ExecuteDecryptsData()
        {
            var transformer = new PgpTransformer(new PgpTransformerOptions
            {
                PrivateKeyText = PrivateKey,
                Password = Password,
                Mode = PgpTransformerMode.Decrypt
            });

            var inStream = new MemoryStream(Encoding.ASCII.GetBytes(CipherText));
            var result = await transformer.Execute(inStream);
            using var reader = new StreamReader(result);
            string output = await reader.ReadToEndAsync();
            Assert.Equal(PlainText, output);
        }

        [Fact]
        public async Task ExecuteEncryptsLargeAmountsOfData()
        {
            var inputFile = await generateData(1);//(1024);
            var encrypt = new PgpTransformer(new PgpTransformerOptions
            {
                PublicKeyText = PublicKey,
                Mode = PgpTransformerMode.Encrypt
            });

            await using var input = File.OpenRead(inputFile);
            var str = await encrypt.Execute(input);

            var decrypt = new PgpTransformer(new PgpTransformerOptions
            {
                PrivateKeyText = PrivateKey,
                Password = Password,
                Mode = PgpTransformerMode.Decrypt
            });

            var result = await decrypt.Execute(str);
            var outputFile = Path.GetTempFileName();
            await using var output = File.OpenWrite(outputFile);
            await result.CopyToAsync(output);
            output.Close();

            Assert.Equal(new FileInfo(inputFile).Length, new FileInfo(outputFile).Length);
        }

        private async Task<string> generateData(int mbCount){
            var tmp = Path.GetTempFileName();
            await using var str = File.OpenWrite(tmp);
            var rnd = new Random();
            for (int i = 0; i < mbCount; i++)
            {
                var buf = new byte[1024 * 1024];
                rnd.NextBytes(buf);
                await str.WriteAsync(buf);
            }
            str.Close();
            return tmp;
        }


        [Fact]
        public async Task ExecuteEncryptsAndDecryptsData()
        {
            var encrypt = new PgpTransformer(new PgpTransformerOptions
            {
                PublicKeyText = PublicKey,
                Mode = PgpTransformerMode.Encrypt
            });

            var ptInput = new MemoryStream(Encoding.ASCII.GetBytes(PlainText));
            var str = await encrypt.Execute(ptInput);

            var decrypt = new PgpTransformer(new PgpTransformerOptions
            {
                PrivateKeyText = PrivateKey,
                Password = Password,
                Mode = PgpTransformerMode.Decrypt
            });

            var result = await decrypt.Execute(str);
            using var reader = new StreamReader(result);
            string output = await reader.ReadToEndAsync();
            Assert.Equal(PlainText, output);
        }

        [Theory]
        [InlineData(PrivateKey, null,"foo", true)]
        [InlineData(null, PublicKey, "foo",false)]
        [InlineData(PrivateKey, PublicKey, "foo", false)]
        [InlineData(PrivateKey, null, null, false)]
        [InlineData(PrivateKey, null, "", false)]
        [InlineData(PrivateKey, PublicKey, "foo",true)]
        public void CtorThrowsIfWrongKeyIsUsed(string privateKey, string publicKey, string password, bool encrypt)
        {
            Assert.Throws<ArgumentException>(() => new PgpTransformer(new PgpTransformerOptions
            {
                PublicKeyText = publicKey,
                PrivateKeyText = privateKey,
                Mode = encrypt ? PgpTransformerMode.Encrypt : PgpTransformerMode.Decrypt,
                Password = password
            }));
        }

        //private static PgpPublicKey ReadPublicKey(Stream inputStream)
        //{
        //    inputStream = PgpUtilities.GetDecoderStream(inputStream);
        //    PgpPublicKeyRingBundle pgpPub = new PgpPublicKeyRingBundle(inputStream);

        //    foreach (PgpPublicKeyRing keyRing in pgpPub.GetKeyRings())
        //    {
        //        foreach (PgpPublicKey key in keyRing.GetPublicKeys())
        //        {
        //            if (key.IsEncryptionKey)
        //            {
        //                return key;
        //            }
        //        }
        //    }

        //    throw new ArgumentException("Can't find encryption key in key ring.");
        //}

        //public static void EncryptPgpFile(string inputFile, string outputFile)
        //{
        //    // use armor: yes, use integrity check? yes?
        //    EncryptPgpFile(inputFile, outputFile, PublicKeyPath, true, true);
        //}

        //public static void EncryptPgpFile(string inputFile, string outputFile, string publicKeyFile, bool armor, bool withIntegrityCheck)
        //{
        //    using (Stream publicKeyStream = File.OpenRead(publicKeyFile))
        //    {
        //        PgpPublicKey pubKey = ReadPublicKey(publicKeyStream);

        //        using (MemoryStream outputBytes = new MemoryStream())
        //        {
        //            PgpCompressedDataGenerator dataCompressor = new PgpCompressedDataGenerator(CompressionAlgorithmTag.Zip);
        //            PgpUtilities.WriteFileToLiteralData(dataCompressor.Open(outputBytes), PgpLiteralData.Binary, new FileInfo(inputFile));

        //            dataCompressor.Close();
        //            PgpEncryptedDataGenerator dataGenerator = new PgpEncryptedDataGenerator(SymmetricKeyAlgorithmTag.Cast5, withIntegrityCheck, new SecureRandom());

        //            dataGenerator.AddMethod(pubKey);
        //            byte[] dataBytes = outputBytes.ToArray();

        //            using (Stream outputStream = File.Create(outputFile))
        //            {
        //                if (armor)
        //                {
        //                    using (ArmoredOutputStream armoredStream = new ArmoredOutputStream(outputStream))
        //                    {
        //                        IoHelper.WriteStream(dataGenerator.Open(armoredStream, dataBytes.Length), ref dataBytes);
        //                    }
        //                }
        //                else
        //                {
        //                    IoHelper.WriteStream(dataGenerator.Open(outputStream, dataBytes.Length), ref dataBytes);
        //                }
        //            }
        //        }
        //    }
        //}
    }

    public static class IoHelper
    {
        public static readonly string BasePath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

        public static Stream GetStream(string stringData)
        {
            MemoryStream stream = new MemoryStream();
            StreamWriter writer = new StreamWriter(stream);
            writer.Write(stringData);
            writer.Flush();
            stream.Position = 0;
            return stream;
        }

        public static string GetString(Stream inputStream)
        {
            string output;
            using (StreamReader reader = new StreamReader(inputStream))
            {
                output = reader.ReadToEnd();
            }
            return output;
        }

        public static void WriteStream(Stream inputStream, ref byte[] dataBytes)
        {
            using (Stream outputStream = inputStream)
            {
                outputStream.Write(dataBytes, 0, dataBytes.Length);
            }
        }
    }
}
