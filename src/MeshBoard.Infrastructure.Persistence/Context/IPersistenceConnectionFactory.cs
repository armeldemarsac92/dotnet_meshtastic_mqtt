using System.Data.Common;

namespace MeshBoard.Infrastructure.Persistence.Context;

internal interface IPersistenceConnectionFactory
{
    DbConnection CreateConnection();
}
