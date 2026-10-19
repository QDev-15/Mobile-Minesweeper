# Ký bản Release (Google Play upload key)

Keystore của Minesweeper là **riêng**, không dùng chung với PdfReader/DocScanner (đúng theo
[MINESWEEPER-PLAN.md](MINESWEEPER-PLAN.md) mục 7). Thư mục `src/Minesweeper/release/` bị gitignore
(`.gitignore` có pattern `[Rr]elease/`): keystore và mật khẩu **không bao giờ** được commit.

## Trạng thái

Keystore đã được tạo (2026-10-19): `src/Minesweeper/release/minesweeper-upload.keystore`,
alias `minesweeper-upload`, `src/Minesweeper/release/Signing.props` đã có đủ `AndroidSigningKeyPass`/
`AndroidSigningStorePass`. Mật khẩu keystore đã được gửi riêng cho dev qua chat lúc tạo — **sao lưu
ngay vào password manager**, không lưu trong file nào khác ngoài `Signing.props`. Mất file keystore +
mật khẩu thì không phát hành bản cập nhật được (nếu bật Play App Signing thì Google cho đặt lại upload
key qua form riêng, nhưng vẫn nên giữ bản sao cẩn thận).

Fingerprint (để đăng ký API key nếu cần, ví dụ Maps/In-App Review):
- SHA1: `D6:D8:71:0C:AA:65:FD:D0:3C:ED:73:8A:00:E8:64:7C:28:AC:68:3D`
- SHA256: `8D:B2:B4:F8:B9:AE:48:EF:93:6E:A0:CB:33:0A:55:E3:AC:0A:59:15:50:B1:4B:2E:01:DE:7D:73:E1:7A:79:6A`

## Nếu cần tạo lại từ đầu (máy khác, hoặc mất file)

Cần JDK (đi kèm Android SDK / Visual Studio), chạy `keytool` trong `src/Minesweeper/release/`:

```
keytool -genkeypair -v -keystore minesweeper-upload.keystore -alias minesweeper-upload \
        -keyalg RSA -keysize 2048 -validity 10000
```

PKCS12 (keystore mặc định của keytool hiện giờ) **không hỗ trợ store password khác key password**
— dùng cùng 1 mật khẩu cho cả hai khi được hỏi.

Tạo `src/Minesweeper/release/Signing.props` (đã bị gitignore):

```xml
<Project>
  <PropertyGroup>
    <AndroidKeyStore>true</AndroidKeyStore>
    <AndroidSigningKeyStore>$(MSBuildThisFileDirectory)minesweeper-upload.keystore</AndroidSigningKeyStore>
    <AndroidSigningKeyAlias>minesweeper-upload</AndroidSigningKeyAlias>
    <AndroidSigningKeyPass>MẬT_KHẨU</AndroidSigningKeyPass>
    <AndroidSigningStorePass>MẬT_KHẨU</AndroidSigningStorePass>
    <AndroidPackageFormat>aab</AndroidPackageFormat>
  </PropertyGroup>
</Project>
```

`Minesweeper.csproj` tự import file này khi build Release nếu nó tồn tại.

## Build bản phát hành

```
dotnet publish src/Minesweeper/Minesweeper.csproj -c Release -f net10.0-android
```

Output: `src/Minesweeper/bin/Release/net10.0-android/com.tonyq.minesweeper-Signed.aab` — file này tải
trực tiếp lên Play Console (mục Production/Testing → Create new release).

Tăng `ApplicationVersion` (versionCode) trong `Minesweeper.csproj` trước mỗi lần tải lên Play.
