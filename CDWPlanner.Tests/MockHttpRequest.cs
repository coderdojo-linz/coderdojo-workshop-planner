using Microsoft.AspNetCore.Http;
using Moq;
using System;
using System.IO;
using System.Text;
using System.Text.Json;

namespace CDWPlaner.Tests
{
    internal class MockHttpRequest : IDisposable
    {
        private readonly MemoryStream stream;
        
        public MockHttpRequest(string body)
        {
            var byteArray = Encoding.UTF8.GetBytes(body);
            stream = new MemoryStream(byteArray);
            stream.Flush();
            stream.Position = 0;

            HttpRequestMock = new Mock<HttpRequest>();
            HttpRequestMock.Setup(x => x.Body).Returns(stream);
        }

        public Mock<HttpRequest> HttpRequestMock { get; }

        public void Dispose()
        {
            stream.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
