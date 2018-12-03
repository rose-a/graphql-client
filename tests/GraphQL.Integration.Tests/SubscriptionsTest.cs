using System;
using System.Diagnostics;
using System.Net.Http;
using System.Net.WebSockets;
using System.Threading.Tasks;
using GraphQL.Client.Http;
using GraphQL.Common.Request;
using GraphQL.Common.Response;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Xunit;


namespace GraphQL.Integration.Tests
{
	public class SubscriptionsTest
	{
		public static IWebHost CreateServer(int port)
		{
			var configBuilder = new ConfigurationBuilder();
			configBuilder.AddInMemoryCollection();
			var config = configBuilder.Build();
			config["server.urls"] = $"http://localhost:{port}";

			var host = new WebHostBuilder()
				.ConfigureLogging((ctx, logging) => logging.AddDebug())
				.UseConfiguration(config)
				.UseKestrel()
				.UseStartup<IntegrationTestServer.Startup>()
				.Build();

			host.Start();

			return host;
		}

		private readonly IWebHost _server;

		public SubscriptionsTest()
		{}

		private GraphQLHttpClient GetGraphQLClient(int port)
			=> new GraphQLHttpClient($"http://localhost:{port}/graphql");


		[Fact]
		public async void AssertTestingHarness()
		{
			var port = 5001;
			using (CreateServer(port))
			{
				var client = GetGraphQLClient(port);

				const string message = "some random testing message";
				var response = await client.AddMessageAsync(message).ConfigureAwait(false);

				Assert.Equal(message, (string)response.Data.addMessage.content);
			}
		}

		private const string SubscriptionQuery = @"
			subscription {
			  messageAdded{
			    content
			  }
			}";
		private readonly GraphQLRequest SubscriptionRequest = new GraphQLRequest
		{
			Query = SubscriptionQuery
		};

		[Fact]
		public async void CanCreateObservableSubscription()
		{
			var port = 5002;
			using (CreateServer(port))
			{
				var client = GetGraphQLClient(port);

				Debug.WriteLine("creating subscription stream");
				IObservable<GraphQLResponse> observable = client.CreateSubscriptionStream(SubscriptionRequest);

				Debug.WriteLine("subscribing...");
				var tester = observable.SubscribeTester();
				const string message1 = "Hello World";

				var response = await client.AddMessageAsync(message1).ConfigureAwait(false);
				Assert.Equal(message1, (string) response.Data.addMessage.content);

				tester.ShouldHaveReceivedUpdate(gqlResponse =>
				{
					Assert.Equal(message1, (string) gqlResponse.Data.messageAdded.content.Value);
				});

				const string message2 = "lorem ipsum dolor si amet";
				response = await client.AddMessageAsync(message2).ConfigureAwait(false);
				Assert.Equal(message2, (string)response.Data.addMessage.content);
				tester.ShouldHaveReceivedUpdate(gqlResponse =>
				{
					Assert.Equal(message2, (string)gqlResponse.Data.messageAdded.content.Value);
				});

				// disposing the client should complete the subscription
				client.Dispose();
				tester.ShouldHaveCompleted();
			}
		}

		[Fact]
		public async void WebSocketErrorsAreThrownThroughAction()
		{
			var port = 5006;
			var callbackTester = new CallbackTester<WebSocketException>();

			// try to connect to host which does not exist
			var client = GetGraphQLClient(port);
			IObservable<GraphQLResponse> observable = client.CreateSubscriptionStream(SubscriptionRequest, callbackTester.CallbackAction);
			var tester = observable.SubscribeTester();
			callbackTester.ShouldHaveInvokedCallback(timeout: TimeSpan.FromSeconds(10));
			tester.ShouldNotHaveReceivedUpdate();

			using (CreateServer(port))
			{
				observable = client.CreateSubscriptionStream(SubscriptionRequest);
				Debug.WriteLine("subscribing...");
				tester = observable.SubscribeTester();
				const string message1 = "Hello World";

				var response = await client.AddMessageAsync(message1).ConfigureAwait(false);
				Assert.Equal(message1, (string)response.Data.addMessage.content);

				tester.ShouldHaveReceivedUpdate(gqlResponse =>
				{
					Assert.Equal(message1, (string)gqlResponse.Data.messageAdded.content.Value);
				});

				const string message2 = "lorem ipsum dolor si amet";
				response = await client.AddMessageAsync(message2).ConfigureAwait(false);
				Assert.Equal(message2, (string)response.Data.addMessage.content);
				tester.ShouldHaveReceivedUpdate(gqlResponse =>
				{
					Assert.Equal(message2, (string)gqlResponse.Data.messageAdded.content.Value);
				});

			}

			callbackTester.ShouldHaveInvokedCallback(timeout: TimeSpan.FromSeconds(10));
			// disposing the client should complete the subscription
			client.Dispose();
			tester.ShouldHaveCompleted(TimeSpan.FromSeconds(10));
		}


		[Fact]
		public async void MultipleSubscriptionsReuseSameWebsocket()
		{
			var port = 5005;
			using (CreateServer(port))
			{
				var client = GetGraphQLClient(port);

				Debug.WriteLine("creating subscription stream");
				IObservable<GraphQLResponse> observable = client.CreateSubscriptionStream(SubscriptionRequest);

				Debug.WriteLine("subscribing...");
				var tester = observable.SubscribeTester();
				const string message1 = "Hello World";

				var response = await client.AddMessageAsync(message1).ConfigureAwait(false);
				Assert.Equal(message1, (string)response.Data.addMessage.content);

				tester.ShouldHaveReceivedUpdate(gqlResponse =>
				{
					Assert.Equal(message1, (string)gqlResponse.Data.messageAdded.content.Value);
				});

				var tester2 = observable.SubscribeTester();

				const string message2 = "lorem ipsum dolor si amet";
				response = await client.AddMessageAsync(message2).ConfigureAwait(false);
				Assert.Equal(message2, (string)response.Data.addMessage.content);
				tester.ShouldHaveReceivedUpdate(gqlResponse =>
				{
					Assert.Equal(message2, (string)gqlResponse.Data.messageAdded.content.Value);
				});
				tester2.ShouldHaveReceivedUpdate(gqlResponse =>
				{
					Assert.Equal(message2, (string)gqlResponse.Data.messageAdded.content.Value);
				});

				// disposing the client should complete the subscription
				client.Dispose();
				tester.ShouldHaveCompleted();
				tester2.ShouldHaveCompleted();
			}
		}

		[Fact]
		public async void CanReconnectWithSameObservable()
		{
			var port = 5003;
			using (CreateServer(port))
			{
				var client = GetGraphQLClient(port);

				Debug.WriteLine("creating subscription stream");
				IObservable<GraphQLResponse> observable = client.CreateSubscriptionStream(SubscriptionRequest);

				Debug.WriteLine("subscribing...");
				var tester = observable.SubscribeTester();
				const string message1 = "Hello World";

				var response = await client.AddMessageAsync(message1).ConfigureAwait(false);
				Assert.Equal(message1, (string)response.Data.addMessage.content);
				tester.ShouldHaveReceivedUpdate(gqlResponse =>
				{
					Assert.Equal(message1, (string)gqlResponse.Data.messageAdded.content.Value);
				});
				Debug.WriteLine("disposing subscription...");
				tester.Dispose();

				Debug.WriteLine("creating new subscription...");
				tester = observable.SubscribeTester();
				const string message2 = "lorem ipsum dolor si amet";
				response = await client.AddMessageAsync(message2).ConfigureAwait(false);
				Assert.Equal(message2, (string)response.Data.addMessage.content);
				tester.ShouldHaveReceivedUpdate(gqlResponse =>
				{
					Assert.Equal(message2, (string)gqlResponse.Data.messageAdded.content.Value);
				});

				// disposing the client should complete the subscription
				client.Dispose();
				tester.ShouldHaveCompleted();
			}
		}

		[Fact]
		public async void CanReconnectAfterServerTimeout()
		{
			var port = 5004;

			var callbackTester = new CallbackTester<WebSocketException>();
			IWebHost webHost = null;

			try
			{
				webHost = CreateServer(port);

				var client = GetGraphQLClient(port);

				Debug.WriteLine("creating subscription stream");
				IObservable<GraphQLResponse> observable = client.CreateSubscriptionStream(SubscriptionRequest, callbackTester.CallbackAction);

				Debug.WriteLine("subscribing...");
				var tester = observable.SubscribeTester();
				const string message1 = "Hello World";

				var response = await client.AddMessageAsync(message1).ConfigureAwait(false);
				Assert.Equal(message1, (string)response.Data.addMessage.content);
				tester.ShouldHaveReceivedUpdate(gqlResponse =>
				{
					Assert.Equal(message1, (string)gqlResponse.Data.messageAdded.content.Value);
				});
				Debug.WriteLine("terminating web host...");
				callbackTester.Reset();
				webHost.StopAsync().GetAwaiter().GetResult();

				Debug.WriteLine("waiting for websocket timeout...");
				callbackTester.ShouldHaveInvokedCallback(timeout: TimeSpan.FromSeconds(10));

				//Debug.WriteLine("restarting web host...");
				//webHost.Dispose();
				//webHost = CreateServer(port); // this fails, so the new connection will break too

				//Debug.WriteLine("creating new subscription...");
				//callbackTester.Reset();
				//tester = observable.SubscribeTester();
				//callbackTester.ShouldHaveInvokedCallback(exception =>
				//{
				//	var test = exception.WebSocketErrorCode;
				//}, TimeSpan.FromSeconds(10));

				//const string message2 = "lorem ipsum dolor si amet";
				//response = await client.AddMessageAsync(message2).ConfigureAwait(false);
				//Assert.Equal(message2, (string)response.Data.addMessage.content);
				//tester.ShouldHaveReceivedUpdate(gqlResponse =>
				//{
				//	Assert.Equal(message2, (string)gqlResponse.Data.messageAdded.content.Value);
				//});

				// disposing the client should complete the subscription
				//client.Dispose();
				//tester.ShouldHaveCompleted(TimeSpan.FromSeconds(10));
			}
			finally
			{
				webHost?.Dispose();
			}
		}
	}
}
