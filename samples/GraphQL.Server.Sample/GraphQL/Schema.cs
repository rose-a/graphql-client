namespace GraphQL.Server.Sample.GraphQL {

	public class Schema : Types.Schema {

		public Schema() {
			this.Query = new QueryRoot();
			this.Subscription = new SubscriptionRoot();
		}

	}

}
