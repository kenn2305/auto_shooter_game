# Prototype Auto Shooter

Game bắn tự động góc nhìn top-down (Unity 6, 2D URP). Chọn 1 trong 3 nhân vật (Iris, Lily, Zena), chọn map
(Whispering Grove / Ancient Ruins), sống sót qua các phase quái ngày càng mạnh, nhặt EXP để lên cấp và
tiến hoá vũ khí.

---

## Chạy file .exe (Windows)

### Yêu cầu
- Windows 10 / 11 bản 64-bit
- Card đồ hoạ hỗ trợ DirectX 11 trở lên
- Khoảng 200 MB trống

### Các bước
1. Lấy thư mục build `Windows` (bản nén `.zip` được chia sẻ, hoặc tự build theo mục **Tự build từ source** bên dưới).
2. Giải nén **toàn bộ** thư mục ra một chỗ bất kỳ, ví dụ `D:\Games\AutoShooter\`. Bên trong phải có đủ:
   ```
   Prototype_Auto_Shooter_Game.exe
   Prototype_Auto_Shooter_Game_Data\      <- bắt buộc, nằm cạnh file .exe
   MonoBleedingEdge\
   UnityPlayer.dll
   UnityCrashHandler64.exe
   ```
   Không copy riêng file `.exe` ra chỗ khác: game sẽ không mở được nếu thiếu thư mục `_Data`.
3. Nhấp đúp `Prototype_Auto_Shooter_Game.exe`.
4. Nếu Windows SmartScreen hiện "Windows protected your PC": bấm **More info** → **Run anyway**
   (file build chưa được ký số nên Windows cảnh báo, không phải virus).
5. Game mở ở **Main Menu** → **START** → chọn nhân vật → chọn map.

### Chạy ở chế độ cửa sổ
Vào **OPTIONS** trong menu để bật/tắt Fullscreen, hoặc chạy bằng dòng lệnh:
```bash
Prototype_Auto_Shooter_Game.exe -screen-fullscreen 0 -screen-width 1280 -screen-height 720
```

---

## Điều khiển

| Phím | Chức năng |
|---|---|
| `W` `A` `S` `D` | Di chuyển |
| Chuột | Hướng tấn công (vũ khí tự bắn theo hướng chuột) |
| `X` | Ultimate (Zena: đổi dạng cận chiến / tầm xa) |
| `Esc` | Tạm dừng / mở menu Pause |
| Mũi tên + `Enter` | Điều hướng menu, chọn nâng cấp khi lên cấp |

Có thể đổi phím trong **OPTIONS → CONTROLS**.

## Mẹo chơi
- Quái mạnh dần theo phase (phút 3, 5, 7, 15, 30) và còn tăng thêm theo từng phút trong phase.
- Elite xuất hiện theo đợt, máu rất trâu nhưng rơi EXP đỏ lớn.
- **Gốc cây (Stump)** đứng yên, rất nhiều máu: hạ được sẽ **chắc chắn rơi Rainbow EXP** hút toàn bộ EXP trên map.
- Đồ ăn rơi trên map hồi máu.
- **SAVE & QUIT** trong menu Pause lưu lại ván đang chơi; **RESUME** ở Main Menu để chơi tiếp.

## Dữ liệu lưu
- Ván đang chơi: `%USERPROFILE%\AppData\LocalLow\DefaultCompany\Prototype_Auto_Shooter_Game\run_save.json`
- Cài đặt và phím: Windows Registry, `HKEY_CURRENT_USER\Software\DefaultCompany\Prototype_Auto_Shooter_Game`
- Xoá các mục trên để chơi lại từ đầu.

## Gặp lỗi?
- Game không mở / màn hình đen: kiểm tra đã giải nén đủ thư mục `_Data` chưa, cập nhật driver card đồ hoạ.
- Log của game nằm ở `%USERPROFILE%\AppData\LocalLow\DefaultCompany\Prototype_Auto_Shooter_Game\Player.log`
  (gửi file này kèm khi báo lỗi).

---

## Tự build từ source

File `.exe` không được đưa lên git (thư mục `Builds/` nằm trong `.gitignore` vì quá nặng). Để tự build:

1. Cài **Unity 6000.3.10f1** qua Unity Hub (kèm module *Windows Build Support (Mono)*).
2. Clone repo và mở thư mục project bằng Unity Hub (lần đầu mở sẽ mất vài phút để import).
3. `File → Build Profiles` → chọn **Windows** → kiểm tra Scene List theo thứ tự:
   `MainMenu`, `Map_01`, `Map_02` (MainMenu phải đứng đầu).
4. Bấm **Build**, chọn thư mục `Builds/Windows`, đặt tên `Prototype_Auto_Shooter_Game.exe`.
5. Chạy file `.exe` vừa tạo như hướng dẫn ở trên. Muốn chia sẻ thì nén **cả thư mục** `Builds/Windows`.

### Ghi chú cho người phát triển
- Bấm Play trong Editor luôn bắt đầu từ Main Menu. Tắt/bật ở `Tools → Menu → Play From Main Menu`
  khi muốn test thẳng một map.
- Chỉ số quái: `Assets/GameContent/Enemies/EnemyConfig.csv` (chạy `Tools → Enemies → Build From EnemyConfig.csv`
  sau khi sửa) và `EnemyPhaseConfig.csv` (chỉ cần Play lại).
- Hướng dẫn cấu hình vũ khí: [Docs/Weapon_Configuration_Guide.md](Docs/Weapon_Configuration_Guide.md).
