using System;
using GraphQL.Types;

namespace GraphQL.Server.Sample.GraphQL {

	public class QueryRoot : ObjectGraphType<object> {

		public QueryRoot() {
			this.Field<NonNullGraphType<DateTimeGraphType>>("dateTime", resolve: context => DateTime.UtcNow);
		}

	}

}
