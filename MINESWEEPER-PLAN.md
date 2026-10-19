# Dò Mìn App — Kế hoạch

> Trạng thái (2026-10-17): mới chỉ lên kế hoạch, **chưa code gì**.
> App thứ 3 của cùng developer (Nguyễn Hữu Quỳnh), cùng Play Console account với DocScanner/PdfReader.
> **Repo riêng** — không ProjectReference ngược về 2 repo kia; phần tái dùng được (AdsService, pattern License/keystore) copy sang.

## 1. Điểm khác biệt so với các app Dò mìn đã có trên Play Store

Thị trường rất bão hoà (hàng nghìn bản clone gần giống nhau), nên không chỉ build lại Minesweeper cổ điển. 2 điểm khác biệt chính, chọn vì giải quyết đúng điểm đau thật của người chơi mà clone thường thiếu:

1. **No-guess mode** — đảm bảo mọi bàn (khi bật chế độ này) giải được 100% bằng suy luận logic thuần, không bao giờép đoán 50/50. Đây là rủi ro kỹ thuật lớn nhất của cả plan (xem mục 3).
2. **Daily Challenge** — mỗi ngày 1 bàn cố định (seed theo ngày, giống nhau cho mọi người chơi), share kết quả dạng text (kiểu Wordle) — cơ chế giữ chân + lan truyền tự nhiên, không cần server riêng.

Bổ sung (ít rủi ro hơn, làm sau No-guess + Daily):
- Hint (gợi ý 1 ô an toàn, giới hạn số lần hoặc Pro)
- Giao diện hiện đại/mượt — mặt bằng chung các clone Minesweeper rất cũ kỹ (kiểu Windows 95), chỉ cần làm đẹp + animation tốt đã là khác biệt rõ.

## 2. Công nghệ

Tái dùng toàn bộ quyết định kỹ thuật đã chứng minh hiệu quả từ PdfReader/DocScanner:

| Việc | Lựa chọn | Lý do |
|---|---|---|
| Framework | .NET MAUI, Android only (chưa có máy Mac cho iOS) | Cùng stack, cùng kinh nghiệm |
| Tách logic thuần C# | `Minesweeper.Core` (không đụng Android, test được trên PC) | Giống `PdfReader.Core`/`DocScanner.Core` — board gen, solver, flood-fill đều test unit được |
| Ads | Copy nguyên `AdsService.Core` + `AdsService.LevelPlay` từ PdfReader (bản mới nhất, đã có retry/watchdog 2026-10-13) | Đổi namespace, đổi AppKey/ad unit ID (tạo app mới trên LevelPlay dashboard) |
| Billing/Pro | Copy pattern `LicenseService`/`ILicenseService` | Đổi `ProProductId` |
| Build Release | LLVM AOT, `MauiXamlInflator=SourceGen`, keystore riêng | Giống 2 app kia, xem `KEYSTORE-README.md` mẫu bên PdfReader |

## 3. Thuật toán cốt lõi — phần khó/rủi ro nhất, cần làm proof-of-concept trước khi code UI

### 3.1 Sinh bàn + first-click-safe
Chuẩn: click đầu tiên không bao giờ trúng mìn, thường còn đảm bảo luôn mở ra được 1 vùng trống (ô số 0) ban đầu để người chơi có gì đó để nhìn — sinh mìn *sau* khi biết ô click đầu tiên, không sinh trước.

### 3.2 No-guess solver — việc khó nhất của toàn bộ app
Sau first click, cần một solver kiểm tra: toàn bộ bàn còn lại có giải được hoàn toàn bằng suy luận logic (không có ô nào buộc phải đoán xác suất) không. Hướng làm:
1. **Suy luận cục bộ** (rẻ, làm trước): với mỗi số đã mở, nếu số ô cờ xung quanh = số đó → các ô chưa mở còn lại quanh nó an toàn; nếu số ô chưa mở xung quanh = số đó → tất cả là mìn. Lặp đến khi không suy ra thêm được gì.
2. **Suy luận chồng lấn giữa các ràng buộc** (tốn hơn): khi 2 ô số có vùng ảnh hưởng giao nhau, trừ các tập ràng buộc cho nhau (subset elimination) để suy thêm — kỹ thuật CSP tiêu chuẩn cho No-guess Minesweeper, không cần SAT solver đầy đủ.
3. Nếu sau bước 1+2 vẫn còn ô không suy luận được → bàn này **không phải No-guess**, sinh lại bàn khác (regenerate), lặp tới khi tìm được bàn hợp lệ hoặc đạt giới hạn số lần thử.

✅ **Đã đo (2026-10-17, `Minesweeper.Core.Tests/NoGuessPerformanceTests.cs`, chạy trên PC dev)**: Dễ 0ms, Vừa 16ms, **Khó (30x16/99 mìn) 48ms** — nhanh hơn hẳn lo ngại ban đầu, kể cả tính thêm biên an toàn cho CPU điện thoại chậm hơn PC. Rủi ro này coi như đã giải quyết: No-guess mode hỗ trợ được cả 3 độ khó, không cần giới hạn Dễ/Vừa. 22/22 unit test pass (gồm cả test "bẫy" solver — bàn 50/50 cố tình dựng tay phải bị nhận diện đúng là không giải được bằng logic thuần).

### 3.3 Daily Challenge
Seed = hash(ngày UTC + độ khó) → sinh bàn xác định (deterministic), giống nhau cho mọi người chơi trong ngày đó. Share kết quả: dùng lại pattern share-sheet đã có sẵn code từ 2 app kia (`IDownloadsService`/share).

### 3.4 Flood-fill mở ô trống
BFS/DFS chuẩn, không có gì đặc biệt.

## 4. Tier tính năng

**Tier 1 — MVP**
- Bàn cổ điển 3 độ khó (Dễ 9x9/10 mìn, Vừa 16x16/40 mìn, Khó 30x16/99 mìn — đúng chuẩn Windows Minesweeper quen thuộc)
- Mở ô, cắm cờ (long-press), chord (chạm đúp ô số đã đủ cờ xung quanh để mở nhanh — thao tác chuẩn, cần thiết kế kỹ vì Minesweeper gốc làm cho chuột 2 nút)
- Timer, đếm mìn còn lại, thắng/thua, chơi lại
- Thống kê cơ bản: thời gian tốt nhất mỗi độ khó (lưu local, không cần tài khoản — giữ đúng tinh thần "privacy-first" đã làm ở PdfReader)

**Tier 2 — điểm khác biệt chính**
- No-guess mode (toggle bật/tắt riêng với bàn thường)
- Daily Challenge + share kết quả dạng text
- Hint (gợi ý 1 ô an toàn — giới hạn hoặc Pro)
- Dark/Light/AMOLED theme, haptic feedback khi mở/cắm cờ/thua

**Tier 3 — về sau**
- Undo (Pro)
- Leaderboard local theo độ khó
- Lưới lục giác/tam giác (đổi hẳn cách suy luận không gian — công sức code cao, để dành nếu app có traction)
- Custom board size

## 5. UI/UX

Giữ phong cách card-based, bo góc đã validate ở PdfReader/DocScanner (dev đã quen làm, cũng quen review). Điểm cần thiết kế kỹ riêng cho game này: **thao tác chạm trên mobile thay cho chuột 2 nút** — long-press = cắm cờ, tap = mở, double-tap ô số đã đủ cờ = chord. Đây là chỗ nhiều clone làm dở (long-press delay khó chịu, dễ bấm nhầm ô cạnh bên trên màn nhỏ) — nên dành thời gian test tay kỹ, có thể thêm nút chuyển "chế độ cắm cờ" ở góc màn hình cho người ngón tay to/bàn nhỏ.

## 6. Ads & Monetization

Giữ nguyên pattern đã chứng minh: LevelPlay, banner hiện khi chưa mua Pro, interstitial **chỉ ở điểm chuyển màn hình tự nhiên** (sau khi thắng/thua, trước khi vào ván mới) — không cắt ngang lúc đang chơi. Pro: tắt ads + Hint không giới hạn + Undo + (có thể) mở sớm lưới lục giác khi ra mắt.

## 7. Việc cần quyết định/chuẩn bị trước khi code

- [ ] Tên app + package id (ví dụ `com.tonyq.minesweeper`, theo đúng pattern `com.tonyq.*` đang dùng)
- [ ] **Làm proof-of-concept riêng cho No-guess solver trước** (console/unit test thuần, chưa đụng UI) — xác nhận tốc độ sinh bàn chấp nhận được trên cả 3 độ khó, trước khi commit sâu vào kiến trúc game
- [ ] Tạo app mới + AppKey + 2 ad unit (banner/interstitial) trên LevelPlay dashboard (không dùng chung ID với 2 app kia)
- [ ] Tạo keystore ký riêng
- [ ] Tạo app mới trên Play Console, xác nhận có bị bắt lại vòng 12 tester/14 ngày không (như đã từng hỏi cho PdfReader)

## 8. Roadmap đề xuất (theo đúng pattern M0–M4 đã dùng)

- **M0 — Solver proof-of-concept**: chỉ phần 3.1–3.2 (sinh bàn + No-guess solver), console/test thuần, đo tốc độ, chưa có UI/MAUI project.
- **M1 — Core game**: Minesweeper project MAUI, bàn chuẩn, mở/cắm cờ/chord, timer, thắng/thua, 3 độ khó.
- **M2 — Điểm khác biệt**: No-guess mode (tích hợp solver từ M0), Daily Challenge + share, Hint.
- **M3 — Polish**: theme, animation, haptic, thống kê local.
- **M4 — Monetization + Play**: AdsService (copy từ PdfReader), Pro IAP, keystore, chuẩn bị Play Console (privacy policy — copy mẫu từ PdfReader, đổi nội dung game).

M0 nên làm **trước tiên và riêng biệt** — nếu solver No-guess không khả thi về tốc độ trên điện thoại thật, cần biết sớm để điều chỉnh phạm vi (ví dụ giới hạn No-guess chỉ ở Dễ/Vừa) trước khi đã lỡ xây UI xung quanh một giả định sai.
