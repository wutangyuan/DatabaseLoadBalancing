# 数据库 Provider 重构说明

当前服务已经将数据库访问拆成两层：

- `DatabaseLoadBalancing.Data.Abstractions`：定义 `IDatabaseProvider` 和 `IDatabaseDialect`，业务执行器只依赖 `System.Data.Common`。
- `DatabaseLoadBalancing.Data`：提供参数绑定、事务、幂等记录和表维护服务，不再直接依赖 `SqliteConnection`。
- `DatabaseLoadBalancing.Data/Sqlite`：提供 SQLite 连接、WAL/busy timeout、SQLite 元数据查询和方言 SQL。

以后增加 SQL Server 时，新增 `DatabaseLoadBalancing.Data.SqlServer` 项目，实现：

1. `IDatabaseProvider.OpenConnection()`，内部使用 `Microsoft.Data.SqlClient`；
2. `IDatabaseDialect` 的建表、表发现、列发现、分页和错误分类；
3. SQL Server 的连接池、事务隔离级别和死锁/锁超时判断；
4. 在 `DatabaseProviderFactory` 增加 `sqlserver` 分支。

HTTP 合同和 WPF 客户端不需要改动。切换点集中在服务端组合根和 Provider 项目，不应把 SQL Server 类型泄漏到 Contracts、Client 或通用 Data 服务中。

当前配置：

```json
{
  "DatabaseProvider": "Sqlite",
  "DatabasePath": ".test-data/database.db"
}
```

SQL Server 适配器完成后配置可以改为：

```json
{
  "DatabaseProvider": "SqlServer",
  "ConnectionString": "Server=localhost;Database=DatabaseLoadBalancing;Trusted_Connection=True;TrustServerCertificate=True"
}
```

