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
5. **Privacy Policy / store listing** — **cập nhật 06/11/2026**: ĐÃ XONG. Trang Google Sites
   (`https://sites.google.com/view/do-min-nprivacypolicy/home`) đã có đầy đủ nội dung đúng, khớp với
   `Minesweeper Privacy Policy.html` và `PrivacyPolicyPage` trong app (xác nhận bằng ảnh chụp màn hình thật
   của owner, sau khi 1 lần kiểm tra bằng WebFetch báo nhầm "trang trống" — WebFetch không chạy JavaScript nên
   không thấy nội dung Google Sites tải động, đừng tin kết quả WebFetch cho domain `sites.google.com`, luôn
   xin ảnh chụp màn hình thật hoặc tự mở trình duyệt để xác nhận). Việc còn lại: dán URL này vào Play Console →
   Policy → Privacy policy (ngoài tầm code, owner tự làm trên Play Console) + chuẩn bị phần store listing
   khác (screenshot, mô tả).
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
- **Máy test thật (Samsung SM-N975F, `RF8M81MKLTA`) là máy DÙNG CHUNG giữa nhiều agent/task trong cùng
  ngày** (xác nhận 2026-11-12: `adb shell dumpsys window | grep mCurrentFocus` lúc đang test Minesweeper
  bất ngờ trả về `com.tonyq.pdfreader` đang có focus — một agent khác đang test PdfReader trên CHÍNH máy
  này cùng lúc). Một lần `adb shell input tap` của phiên này đã vô tình tích vào checkbox của màn hình
  terms-gate bên PdfReader (vô hại — chỉ là toggle UI, không phải mua hàng/xoá dữ liệu — nhưng là lời nhắc
  thật). **Luôn `adb shell dumpsys window | grep mCurrentFocus` để xác nhận đúng app đang có focus TRƯỚC
  MỖI lần `input tap`/`screencap`** khi máy có khả năng đang dùng chung, đừng giả định lệnh `am start` của
  mình là lệnh adb duy nhất chạy trên máy lúc đó.
- **`adb install -r` báo `INSTALL_FAILED_VERSION_DOWNGRADE`** nếu máy đang có bản cài với `versionCode` cao
  hơn `ApplicationVersion` hiện tại trong `.csproj` (gặp 2026-11-12: máy có sẵn versionCode=5 từ lần test
  trước, csproj lúc đó đang là 3). **Đừng gỡ cài đặt để né lỗi này** — dùng `adb install -r -d` (`-d` cho
  phép hạ version) vẫn giữ nguyên toàn bộ dữ liệu app đã lưu, không mất gì, không cần đổi `ApplicationVersion`
  trong `.csproj`.

## 2026-11-12 — Thêm màn hình "terms gate" chặn ở khởi động (đồng ý điều khoản/quảng cáo trước khi vào Home)

Port tính năng đã làm cho PdfReader/DocScanner/LogoExtract cùng ngày, theo đúng idiom riêng của app này
(không có `SplashPage`/`Func<Page>` nào để nối vào — `App.CreateWindow` tự quyết định thẳng giữa
`TermsGatePage` và `AppShell`).

- **File mới**: `src/Minesweeper/Views/TermsGatePage.xaml` + `.xaml.cs` — trang chặn hiển thị khi
  `!AppPreferences.TermsAccepted && !license.State.IsPro`. 3 lối ra: mua Pro (Purchased/AlreadyOwned) → vào
  thẳng, KHÔNG set `TermsAccepted` (để một lần hoàn tiền sau này tự nhiên hiện lại gate thay vì kẹt mãi ở
  cờ cũ); tích checkbox + "Đồng ý & Tiếp tục" → set `TermsAccepted=true`, vào `AppShell` bình thường; "Từ
  chối & Thoát" hoặc nút Back cứng → `Platform.CurrentActivity?.FinishAffinity()`. `OnAppearing` bắn nền
  `license.RefreshAsync()` với timeout ~2.5s (không chặn UI), nếu phát hiện đã Pro (mua từ máy khác) thì tự
  vào thẳng, không set cờ.
- **Sửa**: `src/Minesweeper/App.xaml.cs` (`CreateWindow` tự quyết định `TermsGatePage` hay `AppShell`,
  resolve `ILicenseService` qua `IPlatformApplication.Current?.Services` vì `App` không nhận DI theo
  constructor cho việc này; nếu resolve ra `null` thì fail-open thẳng vào `AppShell` thay vì có rủi ro crash
  mỗi lần mở app vì 1 màn hình đồng ý); `src/Minesweeper/Services/AppPreferences.cs` (thêm
  `TermsAcceptedKey = "terms_accepted_v1"` + property `TermsAccepted`, cùng idiom với `SoundEnabled`/
  `VibrationEnabled` đã có).
- **Chuỗi/đa ngôn ngữ**: app này có ĐẦY ĐỦ 11 file resx (`vi` là file gốc không hậu tố + `en/de/es/fr/id/
  ja/ko/pt/ru/zh-Hans`), khác hẳn giả định ban đầu là "có thể chỉ có 1 ngôn ngữ" — đã thêm 8 khoá
  `TermsGate_*` (Heading/Paragraph1-3/Checkbox/BuyPro/AgreeContinue/DeclineExit) vào **cả 11 file** +
  property tương ứng trong `AppStrings.cs` (file này hand-written, không phải designer tự sinh — xem comment
  đầu file). Quyết định có chủ đích: tên app được "bản xứ hoá" theo đúng quy ước đã có sẵn trong các resx
  khác của chính app này (vd `Minesweeper` tiếng Anh/Đức, `Dò Mìn` tiếng Việt, `Buscaminas` tiếng Tây Ban
  Nha, `扫雷` tiếng Trung...) chứ không viết cứng "Minesweeper" như bản tiếng Việt owner đưa — khớp cách
  `Privacy_Overview_Body` đã làm từ trước. "Pro" được giữ nguyên La-tinh ở hầu hết ngôn ngữ (đúng quy ước
  `Settings_BuyPro` đã có), riêng ja/ko/zh-Hans bản xứ hoá thành "プロ版"/"프로(버전)"/"专业版" vì đó cũng là
  quy ước sẵn có của 3 ngôn ngữ này trong app.
- **Mâu thuẫn ads config đã được dặn KHÔNG tự giải quyết** (giữa comment code "còn placeholder" và
  `RealAdsConfig.cs` có vẻ đã có id thật) — không đụng tới, không cố gỡ mâu thuẫn, đúng như yêu cầu.
  `license.State.IsPro` tắt quảng cáo đúng logic bất kể app key/ad unit nào đang cấu hình.
- **Build**: `dotnet build src/Minesweeper/Minesweeper.csproj -c Debug -nodeReuse:false -m:1` → **0 lỗi**
  (chỉ còn các warning NuGet/binding-generator đã tồn tại từ trước, không phát sinh warning mới ngoài 1 cái
  `CS1734` tự gây ra rồi tự sửa ngay). Một lỗi gặp giữa chừng đáng ghi lại: comment trong
  `TermsGatePage.xaml` ban đầu có 4 chỗ dùng `--` (vd "... exists -- see ...") → `MAUIG1001` lúc parse XAML,
  y hệt bẫy đã ghi ở mục "Bẫy hay gặp" phía trên — sửa thành `—` (em dash) là qua.
- **Test trên máy thật**: publish Release (`dotnet publish ... -c Release -f net10.0-android
  -p:AndroidPackageFormat=apk`) rồi `adb install -r -d` (không uninstall — máy có dữ liệu chơi thật: Dễ
  20 ván/kỷ lục 0:24, Vừa 50 ván/kỷ lục 1:46, Khó 99 ván, Tutorial 29/100 màn/84 sao, xác nhận còn nguyên sau
  khi cài). Đã xác nhận bằng ảnh chụp màn hình thật (dark theme, theo system theme mặc định): gate hiện đúng
  lúc chưa đồng ý; tích checkbox bật đúng nút "Đồng ý & Tiếp tục" (disabled → enabled, đổi cả `Opacity`);
  giá Pro thật lấy được từ Google Play Billing hiện đúng ("Mua Pro ngay · 26.000 đ"); bấm "Đồng ý & Tiếp
  tục" vào đúng `HomePage` với dữ liệu cũ còn nguyên; force-stop + mở lại app **không** hiện gate nữa (cờ
  `TermsAccepted` lưu đúng); `adb logcat -d -b crash` sạch, không crash. **Chưa test**: nút "Mua Pro ngay"
  (sẽ kích hoạt giao dịch Google Play Billing thật trên tài khoản thật của owner — cố tình không bấm, đúng
  tinh thần tránh giao dịch thật như sự cố LogoExtract cùng ngày); nút "Từ chối & Thoát"/nút Back cứng (bỏ
  qua để giảm thao tác trên máy đang dùng chung với agent khác, logic `FinishAffinity()` giống hệt
  `GamePage`/`HomePage` đã dùng ở nơi khác trong chính app này nên rủi ro thấp); light theme (máy test đang
  ở dark theme theo hệ thống, đổi theme OS giữa lúc máy dùng chung có thể ảnh hưởng tới agent kia nên không
  đổi — đã soát bằng mắt cả `Colors.Light.xaml`/`Colors.Dark.xaml` đều có đủ cặp màu `PrimarySoft`/`OffBlack`/
  `Danger`/`Primary` dùng trong trang này).

## 2026-11-12 (tiếp) — Privacy Policy thiếu câu "mua Pro thì không ai thu thập gì", owner tự phát hiện

Lúc giao việc terms-gate, agent build đã được dặn **không đụng** Privacy Policy của app này vì agent khảo sát
trước đó (Explore) báo đã đầy đủ (đã khai đúng Billing + LevelPlay, không có claim sai "không có billing"). Đúng
về nội dung kỹ thuật, nhưng owner tự mở `Minesweeper Privacy Policy.html` lên xem và chỉ ra: **thiếu đúng 1 câu**
mà 3 app kia (LogoExtract/DocScanner/PdfReader) đều mới được thêm cùng ngày — câu nói rõ "mua Pro thì SDK quảng
cáo không còn chạy, nên không ai thu thập gì nữa" (không chỉ "mua Pro thì tắt quảng cáo" như bản cũ đã có).

- `Minesweeper Privacy Policy.html`: thêm 1 đoạn ngay dưới "Tổng quan" (nói thẳng "chính Dò Mìn không thu thập
  gì, chỉ SDK quảng cáo mới có thể, mua Pro thì tắt hẳn SDK đó") + sửa câu dẫn vào bảng bên thứ ba (thêm "chỉ khi
  bạn chưa mua Pro"), đúng khuôn đã dùng cho 3 app kia. Đổi "Ngày cập nhật" 19/10/2026 → 12/11/2026.
- `src/Minesweeper/Views/PrivacyPolicyPage.xaml` (trang trong app): thêm 1 `Label` ngay dưới heading "Bên thứ ba",
  **tái dùng đúng chuỗi `AppStrings.TermsGate_Paragraph3`** (đã dịch sẵn cả 11 ngôn ngữ từ lúc làm terms-gate,
  cùng nội dung "mua Pro thì không ai thu thập gì") — không thêm key `Privacy_*` mới, không phải sửa lại 11 file
  resx lần nữa cho đúng 1 câu đã dịch sẵn. Phải thêm `TextColor="{DynamicResource OffBlack}"` rõ ràng cho label
  này: style `PolicyBody` của trang này không tự đặt `TextColor` (khác `Hint`/implicit Label style của app có đặt),
  IDE báo contrast 1.18:1 nếu không set tay — các Label khác dùng `PolicyBody` trong cùng trang nhiều khả năng
  cũng có vấn đề tương tự nhưng ngoài phạm vi sửa hôm nay (không đổi style chung).
- **Tự mắc đúng bẫy `--` trong XML comment đã ghi sẵn trong mục "Bẫy hay gặp" của chính file này** khi viết
  comment giải thích cho đoạn tái dùng chuỗi trên — build lỗi `InitializeComponent`/`UpdatedLabel` không tồn tại
  (lỗi phái sinh từ XAML không compile được, không phải lỗi logic C#). Sửa `--` thành `—`, build lại sạch.
- Build xác nhận: `dotnet build src/Minesweeper/Minesweeper.csproj -c Debug -nodeReuse:false -m:1` → **0 lỗi**.
- **Chưa cập nhật**: trang Google Sites ngoài (`https://sites.google.com/view/do-min-nprivacypolicy/home`) —
  mục "Việc cần làm tiếp" #5 phía trên ghi là đã khớp nội dung từ 06/11/2026, nhưng giờ nội dung local vừa đổi
  thêm 1 đoạn mới nên **lại lệch với trang ngoài đó** cho tới khi owner tự cập nhật tay (ngoài khả năng sửa từ
  repo, giống tình huống PdfReader/DocScanner cùng ngày).

## 2026-11-18 — Daily Challenge: sửa bug ngày mới mở theo UTC + lưu kỷ lục thời gian theo từng ngày

Owner báo 2 việc cùng lúc: (1) sau 12h đêm (giờ VN) ngày mới của Daily Challenge không mở ngay mà phải đợi tới
gần giữa ngày; (2) Daily Challenge chưa lưu kỷ lục thời gian theo từng ngày như Dễ/Vừa/Khó đã có.

- **Nguyên nhân bug (1)**: `DailyProgressStore.cs`, `DailyCalendarPage.xaml.cs`, và `GamePage.xaml.cs` (chỗ
  `HandleGameEndIfNeeded` ghi nhận thắng Daily) đều tính "hôm nay" bằng `DateOnly.FromDateTime(DateTime.UtcNow)`
  — lệch với label ngày hiển thị trên `HomePage` (`DateTime.Now`, giờ máy). Việt Nam là UTC+7 nên ngày mới của
  Daily chỉ thật sự mở lúc 7:00 sáng giờ địa phương, không phải 0:00 — đúng như owner mô tả "giữa ngày mới mở".
  **Đã sửa**: đổi cả 5 chỗ sang `DateTime.Now` (giờ địa phương máy), để biên ngày khớp đúng nửa đêm người chơi
  thực sự trải qua, không phải nửa đêm UTC.
- **Tính năng (2)**: `DailyProgressStore` thêm `BestTimesKey` (lưu dạng `yyyy-MM-dd:giây`, nối bằng `,`, cùng
  idiom với `CompletedKey` đã có) + 2 method mới: `BestTime(DateOnly)` (đọc kỷ lục ngày đó, null nếu chưa thắng
  ngày đó bao giờ) và `RecordWin(DateOnly, int elapsedSeconds)` (gọi `MarkCompleted` rồi chỉ ghi đè thời gian nếu
  THẤP HƠN kỷ lục cũ của đúng ngày đó — bằng nhau không tính là kỷ lục mới, cùng rule `GameStatsStore.RecordWin`
  đã dùng cho Dễ/Vừa/Khó — trả về `true` nếu vừa lập kỷ lục mới cho ngày đó).
  `GamePage.xaml.cs` (`HandleGameEndIfNeeded`): thay `bool isRecord = !_isDaily && _stats.RecordWin(...)` +
  gọi `_dailyProgress.MarkCompleted(...)` riêng, bằng 1 nhánh `isRecord = _isDaily ? _dailyProgress.RecordWin(dailyDate, _elapsedSeconds) : _stats.RecordWin(...)` —
  Daily giờ cũng hiện đúng chuỗi "Kỷ lục mới!" (`AppStrings.Game_NewRecord`) trên banner kết quả khi phá kỷ lục
  ngày đó, y hệt 3 độ khó khác, **không cần thêm string/resx mới** (tái dùng đúng 2 chuỗi `Game_NewRecord`/
  `Game_TimeResult` đã có sẵn cho cả 11 ngôn ngữ).
- **Build xác nhận**: `dotnet build src/Minesweeper/Minesweeper.csproj -c Debug -nodeReuse:false -m:1` → **0 lỗi**
  (chỉ warning cũ từ `AdsService.LevelPlay`, không phát sinh warning mới). Đã cài bản Release lên Samsung
  SM-N975F (WiFi debug, `adb-RF8M81MKLTA-Y7Cea1._adb-tls-connect._tcp`) — mở được, không crash
  (`adb logcat -d -b crash` sạch, `mCurrentFocus` đúng app).

## 2026-11-19 (tiếp) — DailyCalendarPage: hiện kỷ lục thời gian của từng ngày ngay trên lưới lịch

Owner xem lưới lịch, gửi ảnh chụp màn hình khoanh vùng ngay dưới số ngày (ô ngày 19, đang chọn) và yêu cầu hiện
thời gian hoàn thành của ngày đó ở đúng chỗ đó, đồng thời dời dấu check xanh/chấm đỏ (đang nằm dưới số) lên
phía trên số để nhường chỗ.

- `CalendarDayItem.cs`: thêm `BestTimeSeconds` (int?, không `required` vì ô trống đầu tháng không set) +
  2 property tính sẵn `HasBestTime`/`BestTimeText` (format `m:ss`, cùng kiểu với `ResultDetailLabel` ở
  `GamePage`).
- `DailyCalendarPage.xaml.cs` (`LoadMonth`): mỗi ngày thật gọi thêm `_progress.BestTime(date)` (method mới
  thêm trong `DailyProgressStore` ở mục 2026-11-18 phía trên) để nạp vào `BestTimeSeconds`.
- `DailyCalendarPage.xaml`: đổi thứ tự trong `VerticalStackLayout` của mỗi ô — icon check/chấm đỏ lên TRÊN số
  ngày (FontSize giảm 11→10 / 9→8 để gọn), số ngày ở giữa, thêm 1 `Label` mới cho `BestTimeText` NGAY DƯỚI số
  (FontSize 9, `IsVisible="{Binding HasBestTime}"`, đổi màu trắng khi ô đang chọn giống style số ngày). Tăng
  `HeightRequest` của `Border` mỗi ô từ 52→60 để đủ chỗ cho 4 dòng xếp chồng (icon/chấm, số, giờ) mà không bị
  Border tự clip mất chữ.
  - Một ngày đã hoàn thành NHƯNG chưa có `BestTimeSeconds` (hoàn thành từ trước 2026-11-18, lúc
    `DailyProgressStore` chưa lưu thời gian, chỉ có `MarkCompleted`) sẽ vẫn hiện dấu check xanh như cũ, chỉ
    không có dòng giờ bên dưới (`HasBestTime=false`) — không retroactive, đúng bản chất dữ liệu cũ không có.
  - Tự mắc lại đúng bẫy `--` trong XML comment (đã ghi trong mục "Bẫy hay gặp" của chính file này) khi viết
    comment giải thích cho đoạn đổi thứ tự layout — lỗi `MAUIG1001` + hàng loạt `CS0103` (`InitializeComponent`/
    `Weekday0Label`/...) phái sinh từ XAML không compile được. Sửa `--` thành `—`, build lại sạch.
- Build xác nhận: `dotnet build` → **0 lỗi**. Publish Release + cài qua `adb install -r` lên **BlueStacks**
  (owner tự mở, kết nối qua `adb connect 127.0.0.1:5555` — cổng adb mặc định của BlueStacks, không phải WiFi
  debug thật) — mở app không crash. **Lưu ý khi test tiếp trên máy dùng chung/BlueStacks đang mở sẵn**: lúc
  dùng `adb shell input tap` để tự điều hướng qua các màn hình, phát hiện bàn cờ Daily đang chơi dở tự thay đổi
  (thêm cờ ở nhiều ô mới) giữa các lần chụp màn hình dù chỉ tap đúng 1 lần mỗi lượt — kết luận là owner đang
  trực tiếp chơi ván đó trên chính BlueStacks cùng lúc, **đã dừng hẳn việc tự tap vào bàn cờ** để tránh đụng
  nhầm ô của owner (giống bài học "máy dùng chung" đã ghi với Samsung SM-N975F ở trên, giờ áp dụng luôn cho
  BlueStacks khi owner đang mở sẵn). **Owner tự tay test xong và xác nhận "ok"** (2026-11-19) — không có ảnh
  chụp màn hình cuối cùng xác nhận bằng mắt từ phía agent, dựa hoàn toàn vào xác nhận bằng lời của owner.

## 2026-11-19 (tiếp) — Hard "không thể thắng nổi": giảm mật độ mìn + bật No-guess cho Hard

Owner báo Hard quá khó, không thắng nổi. Điều tra ra đây KHÔNG phải do kỹ năng: Hard là độ khó DUY NHẤT chạy
`BoardGenerator.GeneratePureRandom` (không bảo vệ cả nước đi đầu tiên) ở mật độ 99 mìn/288 ô ≈ **34.4%** — cao
hơn hẳn Expert cổ điển (99/480 ≈ 20.6%). `NoGuessPerformanceTests` trong repo đã tự đo từ 2026-10-21 rằng mật
độ này **thất bại No-guess 10/10 lần** kể cả với ngân sách 400 lần thử thật — tức bàn Hard gần như luôn rơi
vào tình huống phải đoán mù thật sự, và nút Gợi ý (`GameEngine.FindHint`) cũng bó tay vì nó chỉ tìm được ô an
toàn suy luận được bằng logic. Owner chọn làm cả 2 hướng gợi ý (giảm mật độ mìn + bật No-guess cho Hard).

- `Difficulty.cs`: `Hard.MineCount` 99 → **60** (20.8%, đúng bằng `LevelCatalog.MaxDensity` đã có sẵn trong
  code — hằng số này được đo thật từ trước: 60/288 tin cậy 0/10 fail, 65/288 "sát mép vực" nhưng vẫn 0/10,
  70/288 đã fail 2/10 — chọn 60 vì đây là mode chính người chơi dùng thường xuyên nhất, nên ưu tiên độ tin cậy
  hơn là giữ khoảng cách số với Tutorial level 100).
- `GamePage.xaml.cs`: `_noGuess` giờ luôn `true` cho MỌI difficulty (bỏ nhánh ngoại lệ `_level !=
  DifficultyLevel.Hard`) — Hard giờ cũng sinh bàn qua `GenerateNoGuess` (anchor ở tâm bàn), nghĩa là **nước đi
  đầu tiên cũng được bảo vệ an toàn trở lại** cho Hard, hiệu ứng phụ tốt đi kèm (trước đây Hard dùng
  `GeneratePureRandom`, có thể thua ngay cú chạm đầu tiên). Một ván Hard đang chơi dở TỪ TRƯỚC bản cập nhật
  này (lưu với `NoGuess=false`) khi Resume vẫn tiếp tục đúng chế độ cũ nó được sinh ra — không đổi hồi tố.
- **Hệ quả hệ số (đã cố ý giữ, không phải bug)**: `LevelCatalog.MaxDensity` (60/288) và `Difficulty.Hard`
  (giờ cũng 60/288, cùng 12x24) khiến **Tutorial level 100 và Hard giờ là CÙNG MỘT bàn** (size + mine count
  giống hệt) — cùng kiểu quan hệ "intentional, not a coincidence to resolve" mà `Difficulty.cs` đã ghi sẵn
  giữa Medium và `DailyChallenge.Difficulty`. Sửa test `LevelCatalogTests.Level100_MatchesHardsSizeButNotItsMineCount`
  → đổi tên `Level100_MatchesHardsSizeAndMineCount` + assert `MineCount` bằng nhau (trước đó assert `<`,
  giờ sai vì 2 giá trị bằng nhau thật).
- `NoGuessPerformanceTests.cs`: gộp Hard vào `Theory` `GenerateNoGuess_FinishesWithinBudget` (budget 5000ms,
  giống Medium) — Hard giờ PHẢI assert sinh bàn thành công thật sự, không còn là test "measured not asserted"
  nữa (xoá hẳn `GenerateNoGuess_Hard_MeasuredNotAsserted` cũ, không còn ý nghĩa khi Hard đã vào nhóm được
  assert). `LevelCatalog.cs` + comment field `_noGuess` trong `GamePage.xaml.cs` cũng cập nhật lại cho khớp
  thực tế mới (không còn nói "Hard là ngoại lệ" nữa).
- Build + test xác nhận: `dotnet build Minesweeper.slnx` → **0 lỗi**; `dotnet test
  tests/Minesweeper.Core.Tests` → **33/33 pass** (bao gồm case Hard mới trong Theory, xác nhận sinh bàn
  No-guess cho Hard thành công thật, không chỉ lý thuyết).
- **Chưa làm**: chưa cài bản Release lên máy thật/BlueStacks để owner tự tay chơi thử Hard sau khi đổi —
  publish đang chạy nền lúc ghi dòng này, cần cài + chơi thử ít nhất 1 ván Hard để xác nhận cảm giác "khó
  nhưng thắng được" đúng như kỳ vọng, không chỉ dừng ở build/test xanh.
