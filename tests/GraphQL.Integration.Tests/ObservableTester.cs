using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace GraphQL.Integration.Tests
{
	public class ObservableTester<TPayload> : IDisposable
	{
		private readonly IDisposable _subscription;
		private ManualResetEventSlim _updateReceived { get; } = new ManualResetEventSlim();
		private ManualResetEventSlim _completed { get; } = new ManualResetEventSlim();
		private ManualResetEventSlim _error { get; } = new ManualResetEventSlim();

		/// <summary>
		/// The timeout for <see cref="ShouldHaveReceivedUpdate"/>. Defaults to 1 s
		/// </summary>
		public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(1);

		/// <summary>
		/// Indicates that an update has been received since the last <see cref="Reset"/>
		/// </summary>
		public bool UpdateReceived => _updateReceived.IsSet;
		/// <summary>
		/// The last payload which was received.
		/// </summary>
		public TPayload LastPayload { get; private set; }

		public Exception Error { get; private set; }

		/// <summary>
		/// Creates a new <see cref="ObservableTester{T}"/> which subscribes to the supplied <see cref="IObservable{T}"/>
		/// </summary>
		/// <param name="observable">the <see cref="IObservable{T}"/> under test</param>
		public ObservableTester(IObservable<TPayload> observable)
		{
			_subscription = observable.Subscribe(
				obj => {
					LastPayload = obj;
					_updateReceived.Set();
				},
				ex =>
				{
					Error = ex;
					_error.Set();
				},
				() => _completed.Set()
			);
		}

		/// <summary>
		/// Asserts that a new update has been pushed to the <see cref="IObservable{T}"/> within the configured <see cref="Timeout"/> since the last <see cref="Reset"/>.
		/// If supplied, the <paramref name="assertPayload"/> action is executed on the submitted payload.
		/// </summary>
		/// <param name="assertPayload">action to assert the contents of the payload</param>
		public void ShouldHaveReceivedUpdate(Action<TPayload> assertPayload = null, TimeSpan ? timeout = null)
		{
			try
			{
				if (!_updateReceived.Wait(timeout ?? Timeout))
					Assert.True(false, "no update received!");

				assertPayload?.Invoke(LastPayload);
			}
			finally
			{
				Reset();
			}
		}

		/// <summary>
		/// Asserts that no new update has been pushed within the given <paramref name="millisecondsTimeout"/> since the last <see cref="Reset"/>
		/// </summary>
		/// <param name="millisecondsTimeout">the time in ms in which no new update must be pushed to the <see cref="IObservable{T}"/>. defaults to 100</param>
		public void ShouldNotHaveReceivedUpdate(TimeSpan? timeout = null)
		{
			try
			{
				if (_updateReceived.Wait(timeout ?? TimeSpan.FromMilliseconds(100)))
					Assert.True(false, "update was inadvertently pushed!");
			}
			finally
			{
				Reset();
			}
		}

		/// <summary>
		/// Asserts that the subscription has completed within the configured <see cref="Timeout"/> since the last <see cref="Reset"/>
		/// </summary>
		public void ShouldHaveCompleted(TimeSpan? timeout = null)
		{
			try
			{
				if (!_completed.Wait(timeout ?? Timeout))
					Assert.True(false, "subscription did not complete!");
			}
			finally
			{
				Reset();
			}
		}

		/// <summary>
		/// Asserts that the subscription has completed within the configured <see cref="Timeout"/> since the last <see cref="Reset"/>
		/// </summary>
		public void ShouldHaveThrownError(Action<Exception> assertError = null, TimeSpan? timeout = null)
		{
			try
			{
				if (!_error.Wait(timeout ?? Timeout))
					Assert.True(false, "subscription did not throw an error!");

				assertError?.Invoke(Error);
			}
			finally
			{
				Reset();
			}
		}

		/// <summary>
		/// Resets the tester class. Should be called before triggering the potential update
		/// </summary>
		private void Reset()
		{
			//if (_completed.IsSet)
			//	throw new InvalidOperationException(
			//		"the subscription sequence has completed. this tester instance cannot be reused");

			LastPayload = default(TPayload);
			_updateReceived.Reset();
		}

		/// <inheritdoc />
		public void Dispose()
		{
			_subscription?.Dispose();
		}
	}

	public class CallbackTester<TPayload>
	{
		private ManualResetEventSlim _callbackInvoked { get; } = new ManualResetEventSlim();

		/// <summary>
		/// The timeout for <see cref="ShouldHaveReceivedUpdate"/>. Defaults to 1 s
		/// </summary>
		public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(1);
		/// <summary>
		/// The last payload which was received.
		/// </summary>
		public TPayload LastPayload { get; private set; }

		public void CallbackAction(TPayload payload)
		{
			LastPayload = payload;
			_callbackInvoked.Set();
		}

		public void ShouldHaveInvokedCallback(Action<TPayload> assertPayload = null, TimeSpan? timeout = null)
		{
			try
			{
				if (!_callbackInvoked.Wait(timeout ?? Timeout))
					Assert.True(false, $"callback was not invoked within {(timeout ?? Timeout).TotalSeconds} s!");

				assertPayload?.Invoke(LastPayload);
			}
			finally
			{
				Reset();
			}
		}

		public void Reset()
		{
			LastPayload = default(TPayload);
			_callbackInvoked.Reset();
		}
	}

	public static class ObservableExtensions
	{

		public static ObservableTester<T> SubscribeTester<T>(this IObservable<T> observable)
		{
			return new ObservableTester<T>(observable);
		}
	}

}
