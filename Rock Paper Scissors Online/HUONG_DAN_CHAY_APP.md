# Hướng dẫn chạy ứng dụng Rock Paper Scissors Online

Tài liệu này mô tả **điều kiện cần**, **cấu hình**, và **các bước chạy** project ASP.NET Core (Razor Pages + Blazor Server + SignalR + Web API + SQL Server).

---

## 1. Yêu cầu môi trường

| Thành phần | Ghi chú |
|------------|---------|
| **.NET SDK** | **9.0** (khớp `TargetFramework` trong file `.csproj`) |
| **SQL Server** | Local hoặc remote; hỗ trợ Windows Authentication như mặc định trong `appsettings.json` |
| **Hệ điều hành** | Windows (đường dẫn mẫu dưới đây dùng PowerShell) |

Kiểm tra SDK:

```powershell
dotnet --version
```

Kết quả nên là bản **9.x.x**.

---

## 2. Cấu hình cơ sở dữ liệu

Ứng dụng đọc chuỗi kết nối **`ConnectionStrings:DefaultConnection`** từ `appsettings.json` (và có thể ghi đè bằng biến môi trường hoặc `appsettings.Development.json`).

**Mặc định** trong `appsettings.json`:

- **Server:** `.` (SQL Server trên máy local)
- **Database:** `RockPaperScissors`
- **Windows Authentication:** `Trusted_Connection=True`
- **TrustServerCertificate:** `True`

Bạn cần:

1. Cài và chạy **SQL Server** (SQL Server Express / Developer / LocalDB tùy môi trường).
2. Tạo database tên **`RockPaperScissors`** (hoặc đổi tên trong connection string cho khớp).
3. Đảm bảo schema dữ liệu đã được tạo theo quy trình của team (script SQL, migration EF, v.v.). Nếu chưa có DB, ứng dụng có thể **lỗi khi khởi động** hoặc khi gọi API phụ thuộc EF Core.

**Tuỳ chỉnh connection string:** sửa trong `appsettings.json` hoặc chỉ trong `appsettings.Development.json` để không commit secret (nên dùng User Secrets hoặc biến môi trường trên máy cá nhân).

Ví dụ biến môi trường (PowerShell, session hiện tại):

```powershell
$env:ConnectionStrings__DefaultConnection = "Server=.;Database=RockPaperScissors;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true"
```

---

## 3. Cấu hình JWT (bắt buộc khi chạy)

`Program.cs` yêu cầu có **`Jwt:Key`** trong cấu hình. File `appsettings.json` mẫu đã có block `Jwt` (Key, Issuer, Audience, …).

- **Development:** có thể giữ key mẫu trên máy local.
- **Production:** phải dùng **secret mạnh**, không commit lên git; cấu hình qua biến môi trường hoặc secret store.

---

## 4. Đường dẫn & build project

Mở terminal tại **thư mục chứa file `.csproj`** (cùng cấp với `Program.cs`):

```powershell
cd "d:\Netcore\Rock Paper Scissors Online\Rock Paper Scissors Online"
```

Biên dịch:

```powershell
dotnet build
```

Nếu gặp lỗi **MSB3027 / file .exe bị khóa**: đóng mọi instance đang chạy (`dotnet run`, Visual Studio đang debug, hoặc `Rock_Paper_Scissors_Online.exe`), sau đó build lại.

---

## 5. Chạy ứng dụng

### 5.1. Chỉ HTTP (khuyến nghị khi dev đơn giản)

```powershell
dotnet run --launch-profile http
```

- URL: **http://localhost:5266**
- Trình duyệt có thể mở tự động nếu `launchBrowser: true` trong profile.

### 5.2. HTTP + HTTPS

```powershell
dotnet run --launch-profile https
```

- **HTTPS:** https://localhost:7077  
- **HTTP:** http://localhost:5266  

(Lấy từ `Properties/launchSettings.json` — nếu bạn đổi cổng trong file đó thì URL tương ứng thay đổi.)

### 5.3. Visual Studio

1. Mở solution / project.
2. Chọn profile **http** hoặc **https** trên thanh công cụ Run.
3. **F5** (debug) hoặc **Ctrl+F5** (không debug).

---

## 6. Sau khi chạy — mở trang nào?

| URL | Mô tả ngắn |
|-----|------------|
| `http://localhost:5266/` | Trang Razor (marketing / trang chủ tĩnh) |
| `http://localhost:5266/portal` | Trung tâm Blazor — liên kết các module API |
| `http://localhost:5266/lobby` | Sảnh phòng (Blazor + SignalR) |
| `http://localhost:5266/login` | Đăng nhập |

Các route Blazor khác (dashboard, leaderboard, tools, …) có trong menu **mega-menu** trên header hoặc trong `/portal`.

---

## 7. Ghi chú về HTTPS redirect (Development)

Trong môi trường **Development**, nếu chỉ chạy profile **http** (không cấu hình cổng HTTPS), ứng dụng có thể **không** bật redirect sang HTTPS để tránh cảnh báo / lỗi khi không có URL HTTPS. Khi chạy profile **https**, bạn dùng đúng URL HTTPS trong trình duyệt cho cookie / mixed content nếu có.

---

## 8. Kiểm tra nhanh

1. `dotnet build` — không lỗi.
2. `dotnet run --launch-profile http` — log có dòng kiểu **Now listening on: http://localhost:5266**.
3. Mở trình duyệt tới URL trên — trang chủ hoặc `/portal` hiển thị.
4. Nếu lỗi kết nối SQL: kiểm tra SQL Server, tên DB, và connection string.

---

## 9. Tóm tắt lệnh thường dùng

```powershell
cd "d:\Netcore\Rock Paper Scissors Online\Rock Paper Scissors Online"
dotnet build
dotnet run --launch-profile http
```

Dừng server: trong cửa sổ terminal đang chạy app, nhấn **Ctrl+C**.

---

*Nếu bạn bổ sung EF Core Migrations hoặc Docker, nên cập nhật thêm mục “Migration / Container” vào file này cho đồng bộ với pipeline thực tế.*
