# Dò Mìn (Minesweeper) — CLAUDE.md

> Cập nhật: 2026-10-19. Mục đích file này: cho phép tiếp tục làm việc đúng mạch trên **máy khác** —
> đọc file này trước, không phải đoán lại từ đầu. Kế hoạch gốc (ý tưởng, công nghệ, roadmap M0-M4) nằm ở
> [MINESWEEPER-PLAN.md](MINESWEEPER-PLAN.md) — file này chỉ ghi **trạng thái hiện tại** + việc dở dang, không
> lặp lại nội dung plan.

## ⚠️ Chưa có git

Thư mục này **chưa `git init`**, chưa có remote. Nếu "máy khác" không dùng chung ổ đĩa/sync, cần:
- `git init` + tạo remote (GitHub) rồi `git push`, hoặc
- copy thủ công cả thư mục `Mobile-Minesweeper/` (nhớ loại trừ `**/obj/`, `**/bin/` — nặng và tự build lại được).

`src/Minesweeper/release/` (keystore + `Signing.props`) **chưa tồn tại** — xem mục "Việc cần làm tiếp" bên dưới.

## Trạng thái: đã xong, đã test thật trên máy Samsung SM-N975F

- **Core logic** (`src/Minesweeper.Core`, test ở `tests/Minesweeper.Core.Tests`, 34/34 pass):
  `BoardGenerator` (first-click-safe), `NoGuessSolver` (basic rule + subset elimination — đã verify bắt đúng
  trường hợp 50/50 không giải được), `GameEngine` (reveal/flag/chord/win-lose + `Restore()` để resume),
  `DailyChallenge`/`DailySeed` (FNV-1a, không dùng `string.GetHashCode` vì bị randomize per-process),
  `LevelCatalog` (100 level, độ khó nội suy tuyến tính 5x5→30x16 đúng bằng `Difficulty.Hard` ở level 100),
  `LevelStars` (xếp sao theo thời gian, heuristic par-time **chưa tune bằng người chơi thật**).
  Hiệu năng No-guess đã đo thật: Dễ 0ms / Vừa 16ms / Khó(=level 100) 48ms.
- **App MAUI** (`src/Minesweeper`): HomePage (độ khó, No-guess toggle, Thử thách hôm nay, Tutorial, thẻ
  "Ván đang chơi dở"), GamePage (bàn chơi đầy đủ, xem dưới), LevelsPage (lưới 100 màn, khóa/mở, sao),
  SettingsPage (mua Pro qua Play Billing), AboutPage.
- **GamePage — input đã fix 1 bug thật**: ban đầu dùng `TapGestureRecognizer` (single+double tap) +
  `TouchBehavior` (long-press) cùng lúc trên 1 `Border` → **toàn bộ bàn cờ không bấm được** trên máy thật
  (TouchBehavior của CommunityToolkit.Maui chiếm hết touch stream, TapGestureRecognizer chết hẳn). Đã sửa:
  **chỉ dùng 1 `TouchBehavior` duy nhất** (`Command` cho tap, `LongPressCommand` cho long-press), chord
  (double-tap) tự đo thời gian giữa 2 lần `Command` liên tiếp trên cùng ô (xem comment trong
  `GamePage.xaml.cs` — đừng quay lại dùng `TapGestureRecognizer` song song với `TouchBehavior`).
- **Lưu/tiếp tục ván đang chơi**: `SavedGameStore` (JSON qua `Preferences`), auto-save sau mỗi nước đi +
  `OnDisappearing`, xác nhận thoát (`DisplayAlertAsync`) khi đang chơi dở, thẻ "Tiếp tục" trên HomePage.
  Đã xác nhận lưu đúng trên máy thật; màn hình "Tiếp tục" **đã verify qua code review nhưng chưa tự tay bấm
  thành công** lúc test (bị 1 video nổi từ app khác trên máy test che mất nút — không phải lỗi app, xem
  log/lịch sử hội thoại nếu cần chi tiết).
- **Thiết kế lại UI (2026-10-18)**: tạo subagent `mobile-app-designer` (xem
  `../.claude/agents/mobile-app-designer.md`, agent chung cho mọi app MAUI trong workspace này, không riêng
  Minesweeper) sau khi research UI/UX game puzzle di động. Đã áp dụng:
  - **Dark mode đúng chuẩn MAUI**: tách `Colors.xaml` cũ thành `Colors.Light.xaml` + `Colors.Dark.xaml`
    (cùng tên key), `App.xaml.cs` tự merge đúng dictionary theo `RequestedTheme` + lắng nghe
    `RequestedThemeChanged` — mọi page/code-behind đọc màu qua `{StaticResource X}` hay
    `Application.Current.Resources["X"]` tự động đúng theme, không cần sửa từng trang.
  - Sửa 1 bug tiềm ẩn phát hiện lúc thiết kế dark mode: `Number7`/`Number8` ở bản gốc gần như đen/xám đậm
    (đúng cho nền ô mở màu sáng) — nếu giữ nguyên cho dark mode sẽ **vô hình** trên nền ô mở tối màu. Đã
    tách bảng số riêng cho `Colors.Dark.xaml`.
  - Ô bàn cờ: thêm shadow nhẹ cho ô chưa mở (gỡ ngay khi mở ra — tự thân "mất shadow" là tín hiệu "ô này đã
    mở"), `PressedBackgroundColor` trên `TouchBehavior` để có phản hồi tức thì khi chạm (trước khi
    Reveal/Flag kịp chạy xong).
  - LevelsPage: ô khóa mờ hẳn đi (`Opacity` qua binding, không cần converter), viền màu accent nổi bật cho
    đúng 1 ô là "màn nên chơi tiếp theo" (`DataTrigger`).
  - **CHƯA XÁC NHẬN BẰNG MẮT TRÊN MÁY THẬT** — build Release đã chạy xong (`EXITCODE=0`, cài lên máy
    `RF8M81MKLTA` thành công), nhưng phiên làm việc bị ngắt khi đang chờ mở khóa máy/máy mất kết nối USB,
    **trước khi chụp được ảnh xác nhận dark mode hiển thị đúng**. Đây là việc đầu tiên cần làm lại.

## Việc cần làm tiếp (theo thứ tự ưu tiên)

1. **Xác nhận trực quan bản thiết kế mới trên máy thật** — chưa từng thấy dark mode/shadow/LevelsPage mới
   chạy thật, chỉ mới build sạch + cài thành công. Cắm máy, mở app (cả chế độ Light lẫn Dark trong Cài đặt
   hệ thống Android), chụp HomePage/GamePage/LevelsPage, đối chiếu với ý đồ thiết kế ghi trong code comment.
   Nếu có máy đổi theme được, thử đổi theme TRONG LÚC app đang mở để test `RequestedThemeChanged` thật sự
   chạy (chưa test được path này).
2. **Test nút "Tiếp tục" (resume) bằng tay thật** — logic đã có unit test (`Restore_ReproducesExactState`)
   nhưng chưa từng tự tay bấm thành công nút này trên máy (luôn bị cản bởi 1 overlay/video nổi từ app khác
   lúc test qua `adb input tap`). Cần xác nhận bằng ngón tay thật, không qua adb.
3. **LevelPlay chưa có app/ad unit thật** — `LevelPlayConfig.cs` vẫn là `REPLACE_ME_*`. App chạy bình
   thường (không crash, SDK chỉ đơn giản không load được ads), nhưng cần tạo app mới trên LevelPlay dashboard
   trước khi phát hành thật. Theo đúng pattern PdfReader/DocScanner: AppKey riêng, không dùng chung.
4. **Chưa có keystore** — `src/Minesweeper/release/` trống. Theo đúng quy trình đã làm cho PdfReader
   (xem `../Mobile-Pdf-reader/KEYSTORE-README.md` làm mẫu): tạo keystore mới riêng cho Minesweeper, viết
   `Signing.props`. **Không dùng chung keystore với PdfReader/DocScanner.**
5. **Play Billing Pro product ID** — `LicenseService.ProProductId = "minesweeper_pro_remove_ads"` là
   placeholder, cần tạo product thật trên Play Console trước khi build bản Pro thật.
6. **`LevelStars` par-time chưa tune bằng người chơi thật** — công thức hiện tại (~2.5 ô an toàn/giây, sàn
   15s) là heuristic đầu tiên, ghi rõ trong code comment là cần chỉnh lại sau khi có dữ liệu người chơi thật.
7. **Privacy Policy / store listing** — chưa làm cho Minesweeper (đã làm mẫu cho PdfReader, có thể tái dùng
   cấu trúc, đổi nội dung).
8. (Tuỳ chọn, không chặn) — long-press hiện ra **2 lá cờ cùng lúc** khi test bằng `adb shell input swipe`
   mô phỏng giữ ngón tay — nghi là do công cụ test (`adb`) mô phỏng không chuẩn, không phải bug thật (chưa
   tự tay test lại để xác nhận 100%). Nếu tự tay test thấy đúng là bug thật (2 ô bị cắm cờ từ 1 lần giữ
   ngón tay), cần debug lại `TouchBehavior.LongPressCommand` trong `GamePage.xaml.cs`.

## Lệnh build hay dùng

```powershell
# Build + test Core (nhanh, không cần máy/emulator)
dotnet build Minesweeper.slnx -c Debug -nodeReuse:false -m:1
dotnet test tests/Minesweeper.Core.Tests --no-build -c Debug

# Build Release APK (để cài tay test, ký bằng debug key nếu chưa có release/Signing.props)
dotnet publish src/Minesweeper/Minesweeper.csproj -c Release -f net10.0-android -p:AndroidPackageFormat=apk -nodeReuse:false

# Cài lên máy thật (adb trong PATH hoặc dùng full path)
adb install -r "src/Minesweeper/bin/Release/net10.0-android/com.tonyq.minesweeper-Signed.apk"
adb shell am start -n com.tonyq.minesweeper/crc643ce297ba4b79dbfb.MainActivity
```

## Bẫy hay gặp khi làm tiếp (đỡ mất thời gian debug lại)

- **`--` trong XML comment** — lỗi gặp lặp lại RẤT NHIỀU LẦN trong dự án này (csproj, .xaml, .svg đều là
  XML, không chỉ AndroidManifest). XML comment **không được chứa `--`** ở bất kỳ đâu trong nội dung (chỉ
  được ở đúng `<!--`/`-->`). Luôn dùng `:` hoặc `—` (em dash) thay cho `--` khi viết comment giải thích trong
  mọi file `.xaml`/`.csproj`/`.svg`/`AndroidManifest.xml`.
- **`TouchBehavior` + `TapGestureRecognizer` trên cùng 1 view = view chết hẳn** trên Android (xem mục
  GamePage ở trên). Nếu cần thêm gesture mới cho 1 cell, mở rộng `TouchBehavior` hiện có, đừng thêm
  `GestureRecognizers` song song.
- **`adb install` bản Debug hay dính lỗi "Fast Deployment"** (`No assemblies found ... Assuming this is part
  of Fast Deployment. Exiting...` → app crash ngay khi mở). Build/cài bản **Release** để test tay cho chắc
  (Release không dùng Fast Deployment).
- **adb trên Git Bash/MSYS cần `MSYS_NO_PATHCONV=1`** trước lệnh `adb shell screencap`/`pull` với đường dẫn
  kiểu `/sdcard/...`, nếu không MSYS tự ý "sửa" đường dẫn Unix thành đường dẫn Windows sai.
- **`dotnet build`/`publish` Release AOT đa-ABI chạy khá lâu** (vài phút), log hay in 2 vòng số thứ tự
  (x86_64 rồi arm64, hoặc ngược lại) — không phải lỗi, không phải treo máy.
- **Build nhiều lệnh `dotnet` song song dễ đụng độ MSBuild node** (file bị khóa, lỗi `CS2012`/`XARLP7024`).
  Nếu build lỗi kiểu "cannot access file ... used by another process", kiểm tra có Visual Studio/dotnet build
  khác đang chạy không, dùng `-nodeReuse:false -m:1` để build đơn luồng.
