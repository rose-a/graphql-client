using System;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using GraphQL.Resolvers;
using GraphQL.Subscription;
using GraphQL.Types;

namespace GraphQL.Server.Sample.GraphQL {

	public class SubscriptionRoot : ObjectGraphType<object> {

		private readonly ISubject<DateTime> dateTimeSubject = new ReplaySubject<DateTime>(1);

		public SubscriptionRoot() {
			this.AddField(new EventStreamFieldType {
				Name = "dateTime",
				Type = typeof(NonNullGraphType<DateTimeGraphType>),
				Resolver = new FuncFieldResolver<DateTime>(this.Resolve),
				Subscriber = new EventStreamResolver<DateTime>(this.Subscribe)
			});
			Task.Run(() => {
				while (true) {
					Thread.Sleep(TimeSpan.FromSeconds(1));
					this.dateTimeSubject.OnNext(DateTime.UtcNow);
				}
			});
		}

		private DateTime Resolve(ResolveFieldContext context) => DateTime.UtcNow;

		private IObservable<DateTime> Subscribe(ResolveEventStreamContext context) => this.dateTimeSubject.AsObservable();

	}

}
