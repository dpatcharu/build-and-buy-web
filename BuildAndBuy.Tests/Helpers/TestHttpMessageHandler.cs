using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace BuildAndBuy.Tests.Helpers
{
    public class TestHttpMessageHandler : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> _responses = new();

        public void Enqueue(HttpResponseMessage response) => _responses.Enqueue(response);

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (_responses.Count == 0)
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
            return Task.FromResult(_responses.Dequeue());
        }
    }

    public class FakeHttpClientFactory : IHttpClientFactory
    {
        private readonly HttpClient _client;
        public FakeHttpClientFactory(HttpClient client) => _client = client;
        public HttpClient CreateClient(string name = "") => _client;
    }
}
