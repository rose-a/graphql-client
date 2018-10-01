using GraphQL.Server.Sample.GraphQL;
using GraphQL.Server.Ui.GraphiQL;
using GraphQL.Server.Ui.Playground;
using GraphQL.Server.Ui.Voyager;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace GraphQL.Server.Sample {

	public class Startup {

		public IConfiguration Configuration { get; }

		public Startup(IConfiguration configuration) {
			Configuration = configuration;
		}

		// This method gets called by the runtime. Use this method to add services to the container.
		public void ConfigureServices(IServiceCollection services) {
			services.AddSingleton<Schema>();
			services.AddGraphQL(options =>{
				options.EnableMetrics = true;
				options.ExposeExceptions = true;
			})
			.AddWebSockets()
			.AddDataLoader(); 

			services.AddMvc().SetCompatibilityVersion(CompatibilityVersion.Version_2_1);
		}

		// This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
		public void Configure(IApplicationBuilder app, IHostingEnvironment env) {
			if (env.IsDevelopment()) {
				app.UseDeveloperExceptionPage();
			}

			app.UseWebSockets();
			app.UseGraphQLWebSockets<Schema>("/graphql");
			app.UseGraphQL<Schema>("/graphql");
			app.UseGraphiQLServer(new GraphiQLOptions());
			app.UseGraphQLPlayground(new GraphQLPlaygroundOptions());
			app.UseGraphQLVoyager(new GraphQLVoyagerOptions());

			app.UseMvc();
		}

	}

}
