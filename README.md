# DatabaseLoadBalancing

DatabaseLoadBalancing 是一个集中式 SQLite 数据库访问示例：多个 WPF 客户端不直接打开 SQLite 文件，而是通过 TCP 连接控制台服务，由服务端统一执行 SQL、协调写入、记录日志并返回结果。

## 项目特点

- 数据库统一使用 SQLite，数据库文件只由服务端持有。
- 服务端是控制台程序，不提供网页界面，不启动 Kestrel 或 HTTP 网站。
- 客户端通过长度前缀 JSON TCP 协议与服务端通信。
- 服务端集中执行查询、标量查询、数据写入、事务和表记录维护。
- SQLite 写操作由服务端串行协调，支持请求幂等记录和事务回滚。
- 控制台使用 log4net 输出日志：Info 为白色、Warn 为黄色、Error 为红色。
- 客户端使用 WPF，提供数据表列表、记录查看、新增、编辑、删除和清空。
- 当前客户端不直接引用 SQLite，也不接收数据库文件路径。
- 数据库访问层通过 `IDatabaseProvider` 抽象，后续可增加 SQL Server 适配器。

## 项目结构

```text
src/
├─ DatabaseLoadBalancing.Contracts       共享请求、响应和 TCP 协议模型
├─ DatabaseLoadBalancing.Data            数据库访问、SQL 校验、事务和表维护
├─ DatabaseLoadBalancing.Data.Abstractions 数据库提供者和方言抽象
├─ DatabaseLoadBalancing.Server          控制台 TCP 服务和服务端组合根
└─ DatabaseLoadBalancing.Client          WPF 客户端

tests/
├─ DatabaseLoadBalancing.Contracts.UnitTests
├─ DatabaseLoadBalancing.Data.UnitTests
├─ DatabaseLoadBalancing.Server.UnitTests
├─ DatabaseLoadBalancing.Server.IntegrationTests
└─ DatabaseLoadBalancing.Client.UnitTests
```

## 环境要求

- Windows
- .NET 10 SDK
- 可写的服务端数据库目录

## 构建和测试

在仓库根目录执行：

```powershell
dotnet restore DatabaseLoadBalancing.sln
dotnet build DatabaseLoadBalancing.sln --configuration Release
dotnet test DatabaseLoadBalancing.sln --configuration Release --no-build
```

## 启动服务端

服务端是控制台程序，默认监听 `127.0.0.1:5080`，数据库默认位于服务端程序目录下的 `data/database.db`。

```powershell
dotnet run --project src/DatabaseLoadBalancing.Server -- --port 5080
```

也可以指定数据库文件：

```powershell
dotnet run --project src/DatabaseLoadBalancing.Server -- --database .test-data/quickstart.db --port 5080
```

服务端控制台会输出启动状态、客户端请求、SQL 执行结果、耗时和异常日志。按 `Ctrl+C` 可以停止服务。

## 初始化演示表

如果使用全新的 SQLite 文件，可以用一次性演示初始化开关创建两张示例表：

```powershell
dotnet run --project src/DatabaseLoadBalancing.Server -- --database .test-data/quickstart.db --port 5080 --initialize-demo-schema=true
```

该操作使用 `CREATE TABLE IF NOT EXISTS`，不会删除已有数据，创建的表为：

```sql
CREATE TABLE items (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    value TEXT NOT NULL UNIQUE
);

CREATE TABLE audit (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    message TEXT NOT NULL
);
```

## 启动 WPF 客户端

另开一个 PowerShell 窗口执行：

```powershell
dotnet run --project src/DatabaseLoadBalancing.Client --configuration Release
```

在客户端中：

1. 将 TCP 服务地址填写为 `127.0.0.1:5080`。
2. 点击“连接”。
3. 在左侧选择 `items` 或 `audit`。
4. 点击“新增”，输入 JSON 对象，例如：

   ```json
   {
     "value": "第一条数据"
   }
   ```

5. 选择记录后可以编辑或删除，也可以确认清空当前表。

如果数据库中没有业务表，客户端会提示先初始化表结构；服务端内置的 `__request_execution` 表是内部幂等记录表，不会显示给客户端。

## 服务端职责

服务端是数据库文件的唯一业务访问入口，主要负责：

- 校验 SQL 类型、参数和请求内容。
- 通过数据库提供者打开 SQLite 连接。
- 对写入请求进行串行协调，降低 SQLite 写锁冲突。
- 使用事务提交或回滚写入操作。
- 根据 `RequestId` 记录和识别重复请求。
- 返回结构化的执行结果、影响行数、查询列和错误信息。
- 在控制台输出带颜色的请求和执行日志。

## TCP 通信

客户端与服务端使用长度前缀 JSON 消息：

```text
4 字节大端序消息长度 + UTF-8 JSON 消息体
```

支持的主要操作包括：

- `health`
- `tables.list`
- `tables.get`
- `sql.query`
- `sql.scalar`
- `sql.execute`
- `sql.transaction`
- `record.create`
- `record.update`
- `record.delete`
- `table.clear`

服务端控制台不是 SQL 命令行，也不是网站；它只负责运行服务并显示日志。

## 数据库提供者扩展

通用数据层依赖 `IDatabaseProvider` 和 `IDatabaseDialect`。当前实现为 SQLite，后续切换 SQL Server 时可以新增 SQL Server 提供者和方言实现，服务端组合根根据配置选择提供者，客户端和共享协议无需直接引用数据库驱动。

## 日志

log4net 由代码配置，不依赖外部 `log4net.config` 文件，避免部署时配置文件丢失：

- Info：白色
- Warn：黄色
- Error：红色

重定向输出时终端颜色可能不可用，但日志级别文本仍会保留。

## 设计边界

- 一个 SQLite 数据库文件对应一个活动服务端实例。
- 客户端不得直接访问 SQLite 文件。
- 普通 SQL 请求不允许客户端随意修改数据库结构；表结构初始化应由服务端管理员或受控初始化流程完成。
- 当前示例默认绑定本机回环地址 `127.0.0.1`。如需跨主机客户端，需要增加可配置监听地址和相应网络安全控制。

