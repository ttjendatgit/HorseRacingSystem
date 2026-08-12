# Horse Racing Tournament Management System

Hệ thống quản lý giải đua ngựa — nền tảng full-stack giúp số hóa toàn bộ quy trình tổ chức giải đua ngựa từ đăng ký, sắp xếp lịch đua, theo dõi kết quả trực tiếp đến bảng xếp hạng và dự đoán.

## Tech Stack

| Layer | Technology |
|---|---|
| Backend | .NET 9.0 Web API, Entity Framework Core 9.0, SQL Server |
| Frontend | React 19, Vite 8, React Router 7 |
| Auth | JWT Bearer (role-based: 5 roles) |
| API Docs | Swagger / Swashbuckle |

## Actors & Roles

| Role | Responsibilities |
|---|---|
| **Horse Owner** | Đăng ký ngựa, thuê jockey, quản lý danh sách thi đấu, theo dõi kết quả |
| **Jockey** | Nhận lời mời, xác nhận tham gia cuộc đua, theo dõi thành tích cá nhân |
| **Referee** | Kiểm tra ngựa trước đua, ghi nhận vi phạm, lập biên bản, xác nhận kết quả |
| **Spectator** | Xem lịch đua, theo dõi kết quả trực tiếp, dự đoán kết quả |
| **Admin** | Quản lý giải đấu, phân công trọng tài, duyệt đăng ký, công bố kết quả |

## Quick Start

### Prerequisites

- [.NET 9.0 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- [Node.js 18+](https://nodejs.org/)
- SQL Server (local hoặc remote)

### 1. Database Setup

```bash
# Tạo migration đầu tiên
dotnet ef migrations add InitialCreate --project BE

# Áp dụng migration vào database
dotnet ef database update --project BE
```

Cấu hình connection string trong `BE/appsettings.json`.

### 2. Start Backend

```bash
dotnet run --project BE
# API chạy tại https://localhost:5226
# Swagger UI tại https://localhost:5226/swagger
```

### 3. Start Frontend

```bash
cd FE
npm install
npm run dev
# Dev server tại http://localhost:5173
```

Frontend tự động kết nối đến API qua biến môi trường `VITE_API_BASE_URL` (mặc định: `http://localhost:5226`).

## Project Structure

```
HorseRacingSystem/
├── BE/                          # Backend .NET 9.0
│   ├── Controllers/             # API endpoints (mỏng, gọi service)
│   ├── Services/                # Business logic (ServiceResult<T>)
│   │   └── Interfaces/
│   ├── Repositories/            # Data access + Unit of Work
│   │   └── Interfaces/
│   ├── Models/                  # EF entities + Enums
│   ├── Dtos/                    # Request/Response DTOs
│   ├── Data/                    # ApplicationDbContext (17 DbSets)
│   ├── Options/                 # JwtOptions
│   └── Migrations/              # EF Core migrations (gitignored)
│
├── FE/                          # Frontend React 19 + Vite
│   └── src/
│       ├── components/          # Header, Footer
│       ├── pages/               # Mỗi trang = 1 folder (.jsx + .css)
│       └── services/            # API client (authApi.js)
│
├── overview.md                  # Yêu cầu nghiệp vụ (tiếng Việt)
├── CLAUDE.md                    # Hướng dẫn cho AI coding agent
└── AGENTS.md                    # Quy tắc coding cho AI agent
```

## Architecture

```
Client (React) ──REST/JSON──▶ Controllers ──▶ Services ──▶ Repositories ──▶ EF Core ──▶ SQL Server
                                    ▲               ▲              ▲
                                    │               │              │
                              ServiceResult<T>   Interfaces    UnitOfWork
```

- **Controllers** — Mỏng, chỉ điều hướng request đến service và trả về HTTP response.
- **Services** — Chứa toàn bộ business logic. Trả về `ServiceResult<T>` (không throw exception cho lỗi nghiệp vụ).
- **Repositories** — Truy cập dữ liệu qua EF Core. `UnitOfWork` phối hợp nhiều repository trong một transaction.
- **Auth** — JWT Bearer. Token chứa role, dùng để phân quyền 5 cấp.

## Key Design Decisions

- **ServiceResult\<T\> thay vì Exception** — Mọi lỗi nghiệp vụ được biểu diễn qua status code, không dùng exception cho flow control.
- **DeleteBehavior.Restrict toàn bộ** — Không có cascade delete nào, tránh mất dữ liệu liên quan ngoài ý muốn.
- **Migrations gitignored** — Mỗi môi trường tự sinh migration riêng, không share qua git.
- **Enum stored as string** — Tất cả enum lưu dưới dạng string trong DB để dễ đọc.

## Development

```bash
# BE: Build & chạy
dotnet build BE
dotnet run --project BE

# BE: Migration
dotnet ef migrations add <Name> --project BE
dotnet ef database update --project BE

# FE: Dev server, build, lint
cd FE
npm run dev
npm run build
npm run lint
npm run preview
```

## License

Dự án học tập / đồ án cá nhân.
