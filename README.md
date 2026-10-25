# Visual Studio 2022 17.11.2 兼容说明

本代码包针对用户当前的 **Visual Studio Community 2022 17.11.2** 整理。项目目标仍是 **.NET 8**，此 Visual Studio 版本可以使用 .NET 8；无需安装 Visual Studio 2026。

**必须确认：** 已安装 .NET 8 SDK、ASP.NET and web development 工作负载，以及可用的 SQL Server Express 实例。VS「关于」窗口显示的 .NET Framework 4.8 并不代表电脑已经安装 .NET 8 SDK。可在命令提示符执行 `dotnet --list-sdks` 检查。

操作：完整解压 → 双击 `PawPuff.sln` → 等待 NuGet 还原 → 检查 `appsettings.json` 的 SQL Server 实例 → F5 运行。若未安装 SQL Server Express，需要先安装或更改连接字符串。

**验证范围：** 已检查 ZIP 内容与解决方案、项目配置；当前生成环境没有 dotnet SDK，无法实际编译/运行，因此请在本机完成首次构建验证。

---

# PawPuff — Pet Supplies Online Shop

PawPuff 是一个供三人课程小组继续开发的宠物用品电商网站 starter project。技术栈对应课程实践：**Visual Studio 2022、ASP.NET Core MVC、C#、Entity Framework Core 8、SQL Server Express、Razor Views、Bootstrap**。目标框架为 .NET 8。

## 一、运行前准备

请先安装：
- Visual Studio 2022，并在 Visual Studio Installer 中勾选 **ASP.NET and web development（ASP.NET 和 Web 开发）** 工作负载。
- .NET 8 SDK（通常随对应的 Visual Studio 组件安装；可在 Visual Studio Installer 中确认）。
- SQL Server Express，并确认 SQL Server 服务正在运行。
- 建议安装 SQL Server Management Studio（SSMS），用于查看数据库。

默认连接字符串使用 SQL Server Express 实例 `.`\\`SQLEXPRESS`（即常见写法 `Server=\\.\\SQLEXPRESS`）。如果你安装的是 LocalDB 或其他实例，需要修改 `appsettings.json` 的 `ConnectionStrings:DefaultConnection`。

## 二、用 Visual Studio 2022 打开

1. 下载 ZIP 后，右键 ZIP → **全部解压**，解压到一个普通文件夹，例如 `Documents\\PawPuff`。不要直接在压缩包内部打开代码。
2. 打开 Visual Studio 2022。
3. 选择 **Open a project or solution（打开项目或解决方案）**。
4. 进入刚解压的 `PawPuff` 文件夹，选择 `PawPuff.sln`，点击 **Open（打开）**。建议打开 `.sln`，不要只打开单个 `.cs` 文件。
5. 如果 Visual Studio 提示信任文件夹或安装缺失组件，请按提示确认/安装。
6. 等待右下角或输出窗口中的 NuGet package restore（NuGet 包还原）完成。若未自动还原，右键解决方案 → **Restore NuGet Packages（还原 NuGet 程序包）**。
7. 在解决方案资源管理器中，确认 `PawPuff` 项目已加载。右键项目 → **Set as Startup Project（设为启动项目）**（如果它还不是启动项目）。

## 三、首次运行

1. 确认 SQL Server Express 已安装且正在运行；如果数据库连接报错，先检查 `appsettings.json` 的服务器实例名称。
2. 在 Visual Studio 顶部运行按钮旁，选择项目启动配置（通常是 `https` 或 `http`）。
3. 按 **F5**（带调试运行）或 **Ctrl+F5**（不带调试运行）。首次启动会尝试连接 SQL Server，创建初始数据库 `PawPuffDb`，并加入虚构的猫狗商品和角色。
4. 浏览器打开后，先检查首页，再尝试 Shop、商品详情、搜索/筛选、购物车、注册/登录页面。
5. 管理员演示账号：用户名 `ZMXAdmin`，邮箱 `P2983732@my365.dmu.ac.uk`，密码 `ZMXAdmin123!`。这是课程演示账号，不能用于公开部署。启动时会尝试将现有 `ZMXAdmin` 账号的邮箱同步为配置邮箱；不会自动修改密码。

## 四、常见问题

- **找不到 `PawPuff.sln`**：请先完整解压 ZIP，并在解压后的 PawPuff 文件夹中找 `.sln` 文件。
- **NuGet 还原失败**：检查网络连接；在 Visual Studio 菜单中选择 Build → Restore NuGet Packages，之后重新生成解决方案。
- **SQL Server 连接失败 / error 26 / server not found**：确认 SQL Server Express 已安装并启动；检查 `appsettings.json` 的 `Server=...` 是否与本机实例一致。不同电脑的实例名可能不同。
- **出现缺少 .NET 8 / Targeting Pack**：通过 Visual Studio Installer 修改安装，安装 ASP.NET 和 Web 开发工作负载及 .NET 8 组件。
- **页面启动但数据库错误**：检查 SQL Server 服务和连接字符串。当前代码采用首次启动 `EnsureCreated` 初始化数据库的简化方式。
- **修改模型后数据库结构没有更新**：课程实践会学习 EF Core Migrations。当前 starter 为简化首次运行采用 `EnsureCreated`；不要直接把它与迁移流程混用。开始扩展模型前，小组应先统一迁移方案并在干净数据库上验证。

## 五、管理员账号

- 用户名：`ZMXAdmin`
- 邮箱：`P2983732@my365.dmu.ac.uk`
- 演示密码：`ZMXAdmin123!`

可在 `appsettings.json` 中配置 `DemoAdmin:UserName` 和 `DemoAdmin:Email`，密码可用 User Secrets 覆盖。当前项目没有实现邮件发送或邮箱验证；该邮箱用于管理员登录账号标识，不代表网站已能向学生邮箱发送邮件。启动时若更新现有管理员邮箱，会将邮箱验证状态设为未验证。不要将真实密码或 API key 提交到 GitHub。

## 六、当前 starter 已包含

- PawPuff 首页与 Bootstrap 导航布局。
- 虚构猫狗商品样例。
- 商品列表、详情、搜索、宠物类型/类别筛选、价格筛选和分页。
- 带角色限制的商品创建/编辑/删除入口。
- Identity 注册、登录和 Buyer/Seller/Admin 角色。
- 基于 Session 的购物车 starter、订单摘要和结账界面。
- Stripe Checkout 信用卡测试模式入口（需要小组自行配置测试密钥）。

这只是起步框架，不是已完成的考试项目。仍需根据评分标准开发并测试：持久化购物车、完整类别 CRUD、卖家/店铺所有权与管理、管理员禁用用户与操作日志、结账库存并发处理、自动化测试等。

## 七、支付功能边界

只有信用卡支付流程接入 Stripe Checkout 测试模式，并且需要自行配置测试密钥。微信支付和支付宝目前只是页面选项，并没有接入真实支付服务。不要使用真实银行卡信息测试。

## 八、GitHub 小组协作

建议三位组员分别建立 feature branch，通过 Pull Request 合并。提交源代码、项目文件和文档；不要提交 `bin/`、`obj/`、`.vs/`、本机数据库、个人密码、真实用户信息或支付密钥。修改共享模型 `ShopDbContext` 或数据库结构前，先在小组内沟通。

## 九、商品数据与图片

样例商品数据为虚构数据。仅导入你们自己制作、已获授权或许可复用的商品信息与图片；课程要求本身不代表可以任意抓取其他零售网站内容。

## 构建状态说明

此环境没有安装 .NET SDK，因此生成时未能在这里编译验证。请在 Visual Studio 2022 本机执行还原、生成和运行测试；如果出现错误，可把完整错误信息或截图发来继续排查。
