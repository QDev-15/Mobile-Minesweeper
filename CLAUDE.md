# Dò Mìn (Minesweeper) — CLAUDE.md

> Cập nhật: 2026-10-19. Mục đích file này: cho phép tiếp tục làm việc đúng mạch trên **máy khác** —
> đọc file này trước, không phải đoán lại từ đầu. Kế hoạch gốc (ý tưởng, công nghệ, roadmap M0-M4) nằm ở
> [MINESWEEPER-PLAN.md](MINESWEEPER-PLAN.md) — file này chỉ ghi **trạng thái hiện tại** + việc dở dang, không
> lặp lại nội dung plan.

## Git + keystore: đã có (cập nhật 2026-10-19)

Repo đã `git init` + có remote (`git status` báo "up to date with origin/master"). `src/Minesweeper/release/`
đã có keystore riêng (`minesweeper-upload.keystore`, alias `minesweeper-upload`) + `Signing.props` — xem
`KEYSTORE-README.md` ở gốc repo cho mật khẩu/fingerprint/cách tạo lại nếu cần. Thư mục `release/` vẫn bị
gitignore (pattern `[Rr]elease/`) nên mật khẩu không lên git, nhưng **không còn là việc cần làm nữa**.

`Minesweeper.csproj` có target `CopyReleaseAabWithVersion` (owner tự thêm, copy từ DocScanner.csproj) tự
copy `.aab` + `mapping.txt` vào `release\Minesweeper-<version>-<code>\` sau mỗi lần publish Release aab —
không cần copy tay nữa.

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
  - **Đã xác nhận bằng mắt trên máy thật (2026-10-19)**, sau khi vá **2 bug crash thật** phát hiện đúng lúc
    verify (trước đó chỉ build+install thành công, chưa từng bấm mở app nên 2 bug này chưa ai thấy):
    1. `Styles.xaml` dùng `{StaticResource Primary/SurfaceTonal/InkSoft/OffBlack/MilkBackground}` — nhưng
       `App.xaml` merge `Styles.xaml` ngay trong XAML tĩnh (lúc `InitializeComponent()`), còn dictionary màu
       chỉ được merge **sau đó** trong code (`ApplyColorTheme`, chạy sau `InitializeComponent()` trong
       constructor của `App`). `StaticResource` tra cứu ngay lúc parse XAML → luôn throw
       `XamlParseException: StaticResource not found for key Primary`, crash ngay app chưa kịp mở màn hình
       nào. **Đã sửa**: đổi 5 chỗ trong `Styles.xaml` sang `{DynamicResource ...}` (tra cứu trễ, tự cập nhật
       khi dictionary đổi — cũng là điều kiện cần để theme đổi sống được, xem việc 1 bên dưới).
    2. `App.xaml.cs`'s `ApplyColorTheme` tạo dictionary bằng `new ResourceDictionary { Source = new
       Uri(...) }` — MAUI **chỉ cho set `ResourceDictionary.Source` từ XAML**, set bằng code luôn throw
       `InvalidOperationException: Source can only be set from XAML.`. Bug này **crash 100% mọi lần mở app**
       (không phải tình huống hiếm), nghĩa là tính năng dark-mode-merge-theo-code chưa từng chạy được dù
       trước đó ghi "đã xong". **Đã sửa**: thêm `x:Class="Minesweeper.ColorsLight"` /
       `"Minesweeper.ColorsDark"` vào `Colors.Light.xaml`/`Colors.Dark.xaml` + code-behind
       (`Colors.Light.xaml.cs`/`Colors.Dark.xaml.cs`, class rỗng chỉ gọi `InitializeComponent()`),
       `ApplyColorTheme` giờ `new ColorsLight()`/`new ColorsDark()` thay vì set `Source`.
    - Verify: build Debug (`dotnet build -t:Run`) + build Release (publish → bundletool build-apks/install-apks
      qua adb WiFi) đều cài và mở được trên máy Samsung SM-N975F, không crash, dark mode hiển thị đúng màu
      Primary/nền (xem HomePage/SettingsPage lúc verify). **Bài học**: build sạch (`EXITCODE=0`) + cài thành
      công KHÔNG đồng nghĩa app mở được — 2 bug trên đều qua build/install trót lọt, chỉ lộ ra khi thật sự
      bấm mở app / xem logcat `-b crash`. Luôn mở app + check `adb logcat -d -b crash` sau khi cài, đừng dừng
      lại ở "build xong, cài xong".

## Việc cần làm tiếp (theo thứ tự ưu tiên)

1. **Test nút "Tiếp tục" (resume) bằng tay thật** — logic đã có unit test (`Restore_ReproducesExactState`)
   nhưng chưa từng tự tay bấm thành công nút này trên máy (luôn bị cản bởi 1 overlay/video nổi từ app khác
   lúc test qua `adb input tap`). Cần xác nhận bằng ngón tay thật, không qua adb.
   - Còn 1 việc liên quan dark mode chưa test: đổi theme Light/Dark TRONG LÚC app đang mở, xem
     `RequestedThemeChanged` + `{DynamicResource}` mới sửa có tự cập nhật màu sống không (logic đã đúng về
     lý thuyết sau khi sửa 2 bug crash ở trên, nhưng chưa tự tay đổi theme live để xác nhận).
2. **LevelPlay chưa có app/ad unit thật** — `LevelPlayConfig.cs` vẫn là `REPLACE_ME_*`. App chạy bình
   thường (không crash, SDK chỉ đơn giản không load được ads), nhưng cần tạo app mới trên LevelPlay dashboard
   trước khi phát hành thật. Theo đúng pattern PdfReader/DocScanner: AppKey riêng, không dùng chung.
3. **Play Billing Pro product ID** — `LicenseService.ProProductId = "minesweeper_pro_remove_ads"` là
   placeholder, cần tạo product thật trên Play Console trước khi build bản Pro thật.
4. **`LevelStars` par-time chưa tune bằng người chơi thật** — công thức hiện tại (~2.5 ô an toàn/giây, sàn
   15s) là heuristic đầu tiên, ghi rõ trong code comment là cần chỉnh lại sau khi có dữ liệu người chơi thật.
5. **Privacy Policy / store listing** — chưa làm cho Minesweeper (đã làm mẫu cho PdfReader, có thể tái dùng
   cấu trúc, đổi nội dung).
6. (Tuỳ chọn, không chặn) — long-press hiện ra **2 lá cờ cùng lúc** khi test bằng `adb shell input swipe`
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
