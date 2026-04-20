using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace PubQuizCreator.Data
{
    public class AppDbContextFactory
        : IDesignTimeDbContextFactory<AppDbContext>
    {
        #region Public Methods

        public AppDbContext CreateDbContext(string[] args)
        {
            var config = new ConfigurationBuilder()
                .SetBasePath(Path.Combine(Directory.GetCurrentDirectory(), "../PubQuizCreator.Web"))
                .AddJsonFile("appsettings.json")
                .AddJsonFile("appsettings.Development.json", optional: true)
                .Build();

            var connectionString = config.GetConnectionString("Default")
                ?? throw new InvalidOperationException("Connection string 'Default' not found.");

            var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString);
            dataSourceBuilder.UseVector();
            var dataSource = dataSourceBuilder.Build();

            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseNpgsql(dataSource, o => o.UseVector())
                .Options;

            return new AppDbContext(options);
        }

        #endregion Public Methods
    }
}