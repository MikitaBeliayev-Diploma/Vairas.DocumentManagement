using Microsoft.EntityFrameworkCore;
using Npgsql;
using Testcontainers.PostgreSql;
using Vairas.DocumentManagement.Business;
using Vairas.DocumentManagement.Domain;
using Vairas.DocumentManagement.Persistance;
using Vairas.DocumentManagement.Persistance.Migrations;
using Xunit;

namespace Vairas.DocumentManagement.Tests;

[CollectionDefinition("Postgres")] public sealed class PostgresCollection : ICollectionFixture<PostgresFixture>;
