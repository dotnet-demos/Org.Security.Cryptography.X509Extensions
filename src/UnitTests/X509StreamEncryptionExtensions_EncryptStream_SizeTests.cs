﻿using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using X509.EnduranceTest.Shared;

namespace UnitTests
{
    [TestClass]
    public class X509StreamEncryptionExtensions_EncryptStream_SizeTests
    {
        [TestMethod]
        public void WhenSigleLetterAIsEncrypted_ResultSizeWillBe536()
        {
            //Arrange
            const string sampleData = "A";
            var x509EncryptionCert = CertificateLoader.LoadFromFile("TestCertificates/hello.world.2048.net.cer");
            byte[] input = Encoding.UTF8.GetBytes(sampleData);
            //Act
            byte[] output1 = EncryptionDecryptionUtils.EncryptBytes(x509EncryptionCert, input);
            //Assert
            int expectedEncryptedArraySize = 536;
            Assert.AreEqual(expectedEncryptedArraySize, output1.Length, $"Expected encrypted size for letter A is {expectedEncryptedArraySize}, but actual {output1.Length}");
        }

        [TestMethod]
        public void WhenPersonFullNameJoyGeorgeKunjikkuruIsEncrypted_ResultSizeWillBe552()
        {
            //Arrange
            const string fullName = "JoyGeorgeKunjikkuru";
            var x509EncryptionCert = CertificateLoader.LoadFromFile("TestCertificates/hello.world.2048.net.cer");
            byte[] input = Encoding.UTF8.GetBytes(fullName);
            //Act
            byte[] output1 = EncryptionDecryptionUtils.EncryptBytes(x509EncryptionCert, input);
            //Assert
            int expectedEncryptedArraySize = 552;
            Assert.AreEqual(expectedEncryptedArraySize, output1.Length, $"Expected encrypted size for letter A is {expectedEncryptedArraySize}, but actual {output1.Length}");
        }
        [TestMethod]
        public void When8KBIsEncrypted_ResultSizeWillBe8728()
        {
            //Arrange
            const int SampleDataSizeInKB = 8;
            var x509EncryptionCert = CertificateLoader.LoadFromFile("TestCertificates/hello.world.2048.net.cer");
            var inputArray = TestDataGenerator.GenerateJunk(SampleDataSizeInKB);
            //Act
            byte[] output1 = EncryptionDecryptionUtils.EncryptBytes(x509EncryptionCert, inputArray);
            //Assert
            int expectedEncryptedArraySize = 8728;
            Assert.AreEqual(expectedEncryptedArraySize, output1.Length, $"Expected encrypted size for letter A is {expectedEncryptedArraySize}, but actual {output1.Length}");
        }
    }
}

