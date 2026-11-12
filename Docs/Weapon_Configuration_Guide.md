# Hướng dẫn cấu hình vũ khí

Tài liệu này mô tả hệ thống vũ khí đúng theo code hiện tại, kèm từng thao tác trong Unity. Muốn làm một vũ khí từ đầu đến cuối, đọc [mục 3.1](#31-ví-dụ-hoàn-chỉnh-fire-bolt) trước: ví dụ đó đi qua đủ mọi bước, các mục còn lại dùng để tra cứu chi tiết.

**Mục lục**

1. [Hệ thống hoạt động thế nào](#1-hệ-thống-hoạt-động-thế-nào)
2. [Vị trí file trong project](#2-vị-trí-file-trong-project)
3. [Quy trình tạo một vũ khí](#3-quy-trình-tạo-một-vũ-khí)
4. [WeaponData và các level](#4-weapondata-và-các-level)
5. [WeaponAttackDefinition](#5-weaponattackdefinition)
6. [Vũ khí cận chiến (Melee)](#6-vũ-khí-cận-chiến-melee)
7. [Vùng sát thương quanh nhân vật (Aura)](#7-vùng-sát-thương-quanh-nhân-vật-aura)
8. [Vũ khí bắn đạn (Projectile)](#8-vũ-khí-bắn-đạn-projectile)
9. [Vũ khí tia (Beam)](#9-vũ-khí-tia-beam)
10. [Gắn vũ khí vào nhân vật](#10-gắn-vũ-khí-vào-nhân-vật)
11. [Triggered Effects: OnAttack, OnHit, OnKill](#11-triggered-effects-onattack-onhit-onkill)
12. [Status effect và effect asset](#12-status-effect-và-effect-asset)
13. [Bonus chỉ số và luật chỉ số](#13-bonus-chỉ-số-và-luật-chỉ-số)
14. [Đưa vũ khí vào bảng nâng cấp](#14-đưa-vũ-khí-vào-bảng-nâng-cấp)
15. [Công thức tính](#15-công-thức-tính)
16. [Kiểm tra và debug](#16-kiểm-tra-và-debug)
17. [Lỗi thường gặp](#17-lỗi-thường-gặp)
18. [Giới hạn hiện tại của code](#18-giới-hạn-hiện-tại-của-code)
19. [Công thức cấu hình mẫu](#19-công-thức-cấu-hình-mẫu)
20. [Vũ khí đang có trong project](#20-vũ-khí-đang-có-trong-project)
21. [Dạng tối thượng của level cuối](#21-dạng-tối-thượng-của-level-cuối)
22. [Fire Zone và 2 dạng tiến hoá](#22-fire-zone-và-2-dạng-tiến-hoá)
23. [Skill Ultimate của nhân vật](#23-skill-ultimate-của-nhân-vật)

---

## 1. Hệ thống hoạt động thế nào

Một vũ khí được ghép từ các thành phần sau:

```text
WeaponController            Component trên nhân vật: giữ level, đếm cooldown, tạo đòn đánh
  └─ WeaponData             Asset: tên, sát thương gốc, layer mục tiêu, danh sách level
       └─ WeaponLevelData   Một phần tử trong Levels: tốc độ, hệ số damage, kích thước, bonus
            ├─ Attack       Asset WeaponAttackDefinition: đòn đánh của level này
            │    └─ Attack Prefab   Prefab thực thi đòn đánh trong game
            └─ Triggered Effects    Hiệu ứng khi tấn công, khi trúng, khi giết
```

Có 4 loại đòn đánh. Loại được quyết định bởi component trên **Attack Prefab**:

| Loại | Component trên prefab | Asset Attack | Lifetime | Phù hợp với |
|---|---|---|---|---|
| Melee | `WeaponAttackActor` + `AnimationHitboxTrack` | `WeaponAttackDefinition` | Transient | Chém theo animation, hitbox theo từng frame |
| Aura | `WeaponAuraActor` | `WeaponAttackDefinition` | Persistent | Vùng gây sát thương liên tục quanh nhân vật |
| Projectile | `ProjectileWeaponAttackActor` | `ProjectileWeaponAttackDefinition` | Transient | Đạn bay thẳng/cong, xuyên, nảy, nổ |
| Beam | `BeamWeaponAttackActor` + `LineRenderer` | `BeamWeaponAttackDefinition` | Transient | Tia tức thời hoặc chiếu liên tục |

**Transient** tạo một object mới cho mỗi lần đánh rồi tự huỷ. **Persistent** giữ một object tồn tại cùng vũ khí và gọi nó theo chu kỳ.

Mỗi level có Attack riêng, nên một vũ khí có thể đổi hẳn loại đòn đánh giữa các level. Ví dụ một vũ khí có thể chém ở level 1 đến 3 và thành aura ở level 4.

## 2. Vị trí file trong project

```text
Assets/
├─ Scripts/                     Toàn bộ code
│  ├─ CombatStats/              Chỉ số nhân vật (ATK, CRT, Haste...)
│  ├─ Damage/                   DamagePacket, IDamageReceiver, công thức damage
│  ├─ Enemy/                    Spawn, pool, máu, di chuyển của enemy
│  ├─ Experience/               EXP và vật phẩm EXP
│  ├─ FlowField/                Tìm đường
│  ├─ Hitbox/                   AnimationHitboxTrack và cửa sổ Hitbox Editor
│  ├─ LevelUp/                  Bảng chọn nâng cấp
│  ├─ Player/                   Input, di chuyển, ngắm, máu người chơi
│  ├─ Ultimate/                 Skill chủ động của nhân vật
│  └─ Weapons/
│     ├─ Data/                  WeaponData, WeaponLevelData
│     │  └─ Attacks/            Các loại AttackDefinition, ProjectileDefinition
│     ├─ Effects/Data/          Định nghĩa effect và status
│     ├─ Effects/Runtime/       StatusEffectController và phần chạy lúc chơi
│     ├─ Runtime/Core/          WeaponController, tính damage
│     ├─ Runtime/Attacks/       Actor của melee, aura, projectile, beam
│     ├─ Runtime/Projectiles/   Viên đạn và pool
│     ├─ Runtime/TriggeredEffects/  Actor hiệu ứng dạng prefab
│     └─ Editor/                Inspector tuỳ chỉnh
├─ GameContent/                 Toàn bộ dữ liệu cấu hình và prefab
│  ├─ Player/Iris/              Mọi thứ riêng của Iris: prefab, Data, Animation, Sprites, MainWeapon
│  ├─ Player/ Enemies/ Environment/
│  └─ Weapons/
│     ├─ Effects/               Applications, Statuses (tạo Direct khi cần)
│     ├─ Melee/  Aura/          Attacks, Prefabs, Visuals, Weapons
│     └─ Ranged/Projectile/     Prefabs, Visuals (tạo Definitions, Attacks, Weapons khi cần)
├─ Resources/                   Config nạp bằng tên: LevelUpDraftConfig, ExperienceSystemConfig
└─ Scenes/                      TestLevel.unity
```

Quy tắc đặt file:

- Code luôn nằm trong `Scripts`, asset và prefab luôn nằm trong `GameContent`.
- Luôn di chuyển asset **bên trong cửa sổ Project của Unity** để file `.meta` đi cùng và tham chiếu không bị mất.
- Beam chưa có content, nên khi tạo hãy dùng cấu trúc `GameContent/Weapons/Ranged/Beam/{Attacks,Prefabs,Weapons}`.

## 3. Quy trình tạo một vũ khí

Áp dụng cho mọi loại vũ khí:

1. **Tạo Attack Prefab** theo loại vũ khí (mục 6, 7, 8 hoặc 9).
2. **Tạo asset Attack:** trong cửa sổ Project, chuột phải vào folder `Attacks` rồi chọn `Create > Weapon > ...`. Kéo prefab vào trường **Attack Prefab**.
3. **Tạo WeaponData:** chuột phải vào folder `Weapons` rồi chọn `Create > Weapon > Weapon Data`. Điền ID, Base Damage, Attack Interval, Minimum Attack Interval, Area Multiplier và **Target Layer Mask = Enemy**.
4. **Thêm level:** ở danh sách **Levels**, bấm `+`. Kéo asset Attack vào trường **Attack**.
5. **Tách dữ liệu theo level:** trong mục **Full Level Attack Profiles** ở cuối Inspector, bấm **Make Every Level Independent** (xem mục 4.3).
6. **Gắn vào nhân vật** (mục 10) hoặc **đưa vào bảng nâng cấp** (mục 14).
7. **Chạy thử** (mục 16).

Ba trường dễ quên nhất khiến vũ khí không gây sát thương: **Target Layer Mask**, **Base Damage** và **Attack**.

### 3.1 Ví dụ hoàn chỉnh: Fire Bolt

Fire Bolt là súng bắn đạn có 3 level, gây Burn và có 1 dạng tối thượng. Projectile có nhiều lớp nhất nên ví dụ này đi qua đủ mọi bước. Các con số là giá trị khởi đầu và chưa được chạy thử trong game, nên hãy cân bằng lại sau khi test.

**Bước 1: Prefab viên đạn**

1. `Create Empty`, đặt tên `FireBoltProjectile`.
2. Thêm `Weapon Projectile Actor` và `Sprite Renderer`. Vẽ đầu đạn hướng `+X`. Không cần `Rigidbody2D` hay `Collider2D`.
3. Lưu prefab vào `GameContent/Weapons/Ranged/Projectile/Prefabs/`.

Prefab điều phối thì dùng lại `Ranged/Projectile/Prefabs/ProjectileAttackActor.prefab`, không cần tạo mới.

**Bước 2: ProjectileDefinition**

`Create > Weapon > Projectile > Definition`, đặt tên `FireBoltProjectileDefinition`:

| Trường | Giá trị |
|---|---|
| Projectile Prefab | `FireBoltProjectile` |
| Speed / Lifetime | `14` / `3` |
| Maximum Range | `14` |
| Collision Radius | `0.15` |
| Obstacle Layer Mask | `Ostacle`, không chọn `Enemy` |
| Trajectory | `Straight` |
| Impact Mode | `PierceAll` |
| Maximum Pierced Targets | `1`, đạn biến mất sau mục tiêu đầu tiên |

**Bước 3: Attack**

`Create > Weapon > Attack > Projectile`, đặt tên `FireBoltAttack`:

| Trường | Giá trị |
|---|---|
| Lifetime | `Transient` |
| Attack Prefab | `ProjectileAttackActor` |
| Projectile | `FireBoltProjectileDefinition` |
| Shot Mode / Spawn Pattern | `Shot` / `Forward` |
| Per Projectile Damage Multiplier | `1` |
| Hit Policy | `OncePerProjectile` |

**Bước 4: WeaponData**

`Create > Weapon > Weapon Data`, đặt tên `FireBolt`:

| Trường | Giá trị |
|---|---|
| Weapon ID | `fire_bolt`, không đổi về sau |
| Display Name, Icon | Hiện trên thẻ nâng cấp |
| Base Damage | `12` |
| Critical Hit Multiplier | `1.75` |
| Damage Variance Percent | `0` khi test, sau đó `10` |
| Affected By Haste | Bật |
| Target Layer Mask | `Enemy` |
| Attack Interval / Minimum Attack Interval | `1.2` / `0.2` |
| Area Multiplier | `1` |

**Bước 5: Levels**

Thêm 3 phần tử ở **Levels** và kéo `FireBoltAttack` vào cả 3. Bấm **Make Every Level Independent** ở cuối Inspector, rồi sửa Attack của từng level trong mục **Complete Attack Configuration** ngay tại đó.

| Level | Attack | Stat Bonuses | Triggered Effects |
|---|---|---|---|
| 1 | `Shot`, `Forward`, hệ số `1` | Trống | Trống |
| 2 | `Multishot`, `Cone`, 3 viên, 30°, hệ số `0.6` | Attack Speed Percent `15` | Trống |
| 3 | `Multishot`, `Cone`, 5 viên, 50°, hệ số `0.6` | Attack Power Percent `20` | OnHit gây Burn (bước 6) |

Điền **Description** cho mỗi level. Stat Bonuses cộng dồn qua các level và tác dụng lên **mọi vũ khí** của nhân vật. Triggered Effects không kế thừa, level sau muốn giữ thì phải thêm lại.

Với chỉ số gốc của Iris (ATK `120`, Haste `7`) và chưa có bonus nào khác:

| Level | Damage mỗi viên | Interval |
|---|---|---|
| 1 | `12 × 1.2 × 1 = 14` | `1.2 / 1.07 = 1.12` giây |
| 2 | `12 × 1.2 × 0.6 = 9` | `1.2 / 1.23 = 0.98` giây |
| 3 | `12 × 1.44 × 0.6 = 10` | `0.98` giây |

**Bước 6: Gây Burn**

Ở level 3, thêm một dòng **Triggered Effects**:

| Trường | Giá trị |
|---|---|
| Trigger | `OnHit` |
| Trigger Chance | `1` |
| Effect | `Effects/Applications/ApplyBurnEffect` |
| Effect Prefab | Trống |

Muốn dùng status khác, xem mục 12.

**Bước 7: Dạng tối thượng**

1. Trong cửa sổ Project, chọn `Aura/Prefabs/FireGroundZoneActor`, nhân bản bằng `Ctrl+D`, đặt tên `FireBoltZoneActor` và đặt **Damage Multiplier** của `Weapon Aura Actor` là `0.4`. Hệ số này độc lập với hệ số của viên đạn, và các vùng cháy chồng lên nhau được, nên bản gốc `1.5` sẽ quá mạnh với vũ khí bắn nhiều viên.
2. Tạo `FireBoltInfernoProjectileDefinition`: giống bước 2, đổi Impact Mode thành `ExplodeOnFirstEnemy`, Explosion Radius `2.5`, Explosion Damage Multiplier `1`, Ground Zone Prefab = `FireBoltZoneActor`.
3. Tạo `FireBoltInfernoAttack`: giống bước 3, đổi Projectile thành asset vừa tạo, `Multishot`, `Cone`, 3 viên, 30°, hệ số `0.8`.
4. Trong WeaponData, ở **Forms**, bấm `+` và điền:

| Trường | Giá trị |
|---|---|
| Evolution ID | `inferno_bolt`, bắt buộc |
| Display Name, Description | Hiện trên thẻ chọn dạng |
| Attack | `FireBoltInfernoAttack` |
| Triggered Effects | Thêm lại dòng OnHit gây Burn. Hit của vùng cháy cũng kích hoạt dòng này |

Nút ở bước 5 không áp dụng cho Forms, nên Attack của dạng tối thượng phải tạo riêng như trên. Để trống **Attack** thì dạng này dùng Attack của level 3.

`ExplodeOnFirstEnemy` chỉ nổ khi chạm kẻ địch, bay hết tầm thì biến mất. Muốn luôn nổ tại điểm đến thì dùng `ExplodeAtEnd`.

**Bước 8: Đưa vào game**

1. Chọn `Assets/Resources/LevelUpDraftConfig`.
2. Ở **Weapons**, bấm `+`, kéo `FireBolt` vào **Weapon Data**, đặt **Offer Weight** `1` và **Minimum Player Level** `2`. Level 1 không phải Aura nên để trống **Acquisition Prefab**.
3. **Maximum Weapon Slots** đang là `2` và Slash của Iris đã chiếm một slot. Tăng lên `3` nếu muốn dùng cùng lúc Slash, Fire Zone và Fire Bolt.

**Bước 9: Chạy thử**

1. Vào Play Mode, chọn object có `Level Up Draft Controller` và bấm **Open Draft For Current Level** để nhận vũ khí.
2. Chọn `Weapon Controller` của Fire Bolt, bấm **Debug Level Up** đến level 3, rồi bấm **Evolve: Tên dạng** ở mục **Debug Evolution**.
3. Đối chiếu log `[DAMAGE]` trong Console với bảng ở bước 5.

**Áp dụng cho các loại khác**

| Loại | Attack Prefab | Asset Attack | Khác với ví dụ | Chi tiết |
|---|---|---|---|---|
| Melee | `Weapon Attack Actor`, `Animation Hitbox Track`, `Animator`, `Sprite Renderer` | `Weapon > Attack Definition`, Transient, **Animator State Name** khớp đúng tên state | Vẽ hitbox theo từng frame. Không có hệ số damage theo level | Mục 6 |
| Aura | `Weapon Aura Actor`, `Circle Collider 2D` là trigger | `Weapon > Attack Definition`, **Persistent**, đặt **Hit Cooldown** | Nhịp đánh là Hit Cooldown, không dùng Attack Interval, không chịu Haste. Hệ số damage nằm trên prefab. Cần Acquisition Prefab nếu level 1 là Aura | Mục 7 |
| Beam | `Beam Weapon Attack Actor`, `Line Renderer` | `Weapon > Attack > Beam`, Transient | Cần **Beam Material**. Project chưa có content Beam để tham khảo | Mục 9 |

## 4. WeaponData và các level

**Tạo:** `Create > Weapon > Weapon Data`. **Lưu tại:** `GameContent/Weapons/<Loại>/Weapons/`.

### 4.1 Trường chung

| Trường | Ý nghĩa | Gợi ý |
|---|---|---|
| Weapon ID | ID duy nhất. Bảng nâng cấp dùng ID này để nhận ra vũ khí đã sở hữu | `snake_case`, không đổi về sau, ví dụ `iris_slash` |
| Display Name | Tên hiện trên thẻ nâng cấp | |
| Icon | Ảnh trên thẻ nâng cấp | Sprite bất kỳ |
| Base Damage | Sát thương gốc | Xem công thức ở mục 15 |
| Critical Hit Multiplier | Hệ số khi chí mạng, tối thiểu 1 | `1.75` là 175% |
| Damage Variance Percent | Độ dao động damage | `10` là 90% đến 110%. Đặt `0` khi cân bằng |
| Affected By Haste | Haste có rút ngắn Attack Interval và tăng tốc animation không | Tắt nếu cần nhịp cố định |
| Target Layer Mask | Layer nhận sát thương | **Chọn `Enemy`**, không chọn `Everything` |
| Attack Interval | Số giây giữa hai lần đánh, cố định cho mọi level | Chỉ dùng cho Transient. Aura dùng Hit Cooldown |
| Minimum Attack Interval | Sàn của interval sau khi tính Haste | `-1` là không giới hạn |
| Area Multiplier | Hệ số kích thước của đòn đánh, cố định cho mọi level | `1` là mặc định. **Đừng đặt `0`**: đòn đánh sẽ có kích thước 0 và không trúng ai |
| Levels | Danh sách level | Cần ít nhất 1 phần tử |

### 4.2 Trường của mỗi level

| Trường | Ý nghĩa | Lưu ý |
|---|---|---|
| Description | Mô tả trên thẻ nâng cấp | Nên điền, nếu trống thẻ hiện chữ mặc định |
| Stat Bonuses | Bonus cho nhân vật khi đạt level này | Cộng dồn với các level trước (mục 13) |
| Stat Restrictions | Luật chặn hoặc đổi chỉ số | Mục 13 |
| Attack | Asset đòn đánh của level | Bắt buộc |
| Triggered Effects | Hiệu ứng của level | Không tự kế thừa. Muốn level sau vẫn có thì phải thêm lại |

**Levels** chỉ chứa level thường. Nếu vũ khí có dạng tối thượng, level tối đa là danh sách dạng đứng ngay sau level thường cuối cùng (mục 21). Nếu không có, level thường cuối cùng là level tối đa.

Level không còn trường Damage, Attack Interval, Minimum Attack Interval và Area Multiplier. Muốn mạnh hơn, đánh nhanh hơn hoặc đòn đánh to hơn khi lên level, hãy thêm **Stat Bonuses**:

- **Attack Power Percent** để tăng damage.
- **Attack Speed Percent** để giảm interval. Vũ khí cần bật **Affected By Haste**, và interval không xuống dưới **Minimum Attack Interval**.
- **Weapon Area Percent** để tăng kích thước.

Các bonus này cộng vào chỉ số của nhân vật, nên có tác dụng lên **mọi vũ khí** đang trang bị.

Muốn tăng damage riêng cho một vũ khí theo level, hãy đổi hệ số trong Attack của level đó: **Per Projectile Damage Multiplier** với Projectile, **Damage Multiplier Per Tick** với Beam. Aura dùng **Damage Multiplier** trên `Weapon Aura Actor`, nên mỗi mức damage cần một prefab riêng. Melee chưa có hệ số này.

### 4.3 Attack Profile riêng cho từng level

Asset Attack có thể được dùng chung bởi nhiều level. Khi đó sửa một chỗ sẽ đổi tất cả. Mục **Full Level Attack Profiles** ở cuối Inspector giúp tách chúng:

| Nút | Tác dụng |
|---|---|
| **Make Every Level Independent** | Nhân bản Attack của mọi level thành sub-asset nằm trong WeaponData |
| **Make Complete Independent Copy For This Level** | Làm việc đó cho một level |
| **Copy Complete Profile From Previous Level** | Hiện khi level chưa có Attack: sao chép từ level trước |
| **Make Projectile Definition Independent** | Tách riêng ProjectileDefinition đang dùng chung |

Sau khi tách, bạn chỉnh toàn bộ Attack và Projectile ngay trong Inspector của WeaponData. Asset Attack ban đầu trong folder `Attacks` chỉ còn là bản mẫu để sao chép.

## 5. WeaponAttackDefinition

Đây là phần chung của mọi loại Attack.

**Tạo:** `Create > Weapon > Attack Definition` cho Melee và Aura. Projectile và Beam có menu riêng ở mục 8 và 9.

| Trường | Ý nghĩa | Melee | Aura | Projectile | Beam |
|---|---|---|---|---|---|
| Attack ID, Display Name | Tên để nhận biết | ✓ | ✓ | ✓ | ✓ |
| Lifetime | Transient hoặc Persistent | Transient | **Persistent** | Transient | Transient |
| Attack Prefab | Prefab thực thi | ✓ | ✓ | ✓ | ✓ |
| Animator State Name | Tên state trong Animator, phải khớp tuyệt đối | ✓ | | | |
| Hit Limit | Số mục tiêu tối đa mỗi lần đánh, `-1` là không giới hạn, `0` là không trúng ai | ✓ | ✓ | | |
| Hit Cooldown | Chu kỳ gây sát thương | | ✓ | | |
| Hit Rule | **Chưa có tác dụng** (mục 18) | | | | |
| Bonus Damage Status | Mục tiêu đang có status này nhận thêm damage | ✓ | ✓ | ✓ | ✓ |
| Bonus Damage Percent | Phần trăm damage thêm, `20` là +20% | ✓ | ✓ | ✓ | ✓ |

## 6. Vũ khí cận chiến (Melee)

Cần có: sprite animation, Animator, prefab có hitbox theo frame, và asset Attack.

### 6.1 Chuẩn bị animation

1. Chọn file sprite sheet. Trong Inspector đặt **Texture Type = Sprite (2D and UI)**, **Sprite Mode = Multiple**, rồi bấm **Apply**.
2. Bấm **Sprite Editor**, chọn **Slice**, cắt theo Grid hoặc Automatic, rồi bấm **Apply**.
3. Mở rộng file sprite trong Project, chọn các frame và kéo vào Scene. Unity sẽ hỏi tên để lưu file `.anim` và tự tạo Animator Controller.
4. Lưu cả hai vào `GameContent/Weapons/Melee/Visuals/<TênVũKhí>/`, rồi xoá object vừa tạo trong Scene.
5. Chọn file `.anim` và **tắt Loop Time**, vì đòn chém chỉ chạy một lần.
6. Mở Animator Controller (`Window > Animation > Animator`) và ghi lại **đúng tên state**, ví dụ `Slash`.

**Hướng vẽ:** cạnh trên của sprite là hướng ngắm. Nhân vật xoay vũ khí sao cho trục `+Y` hướng về con trỏ chuột.

### 6.2 Tạo Attack Prefab

1. Trong Hierarchy chọn `Create Empty`, đặt tên ví dụ `MySlashAttackActor`.
2. **Add Component** `Sprite Renderer`. Gán sprite đầu tiên và chỉnh **Order in Layer** để hiện trên nhân vật.
3. **Add Component** `Animator`. Kéo Controller ở trên vào **Controller**, tắt **Apply Root Motion**.
4. **Add Component** `Weapon Attack Actor`. Unity tự thêm `Animation Hitbox Track`.
5. Trong `Weapon Attack Actor`, kéo Track vào **Hitbox Track**, Animator vào **Animator**. **Cleanup Delay** là thời gian chờ thêm sau khi clip kết thúc rồi mới huỷ object.
6. Trong `Animation Hitbox Track`:
   - **Animator** và **Animation Clip**: gán đúng clip ở trên.
   - **Detection Mode = Physics2D**.
   - **Activation Mode = FramesWithHitboxes**: frame nào có hitbox thì frame đó gây sát thương.
   - **Target Layer**: để trống cũng được, lúc chạy sẽ lấy từ WeaponData.
7. Kéo object từ Hierarchy vào `GameContent/Weapons/Melee/Prefabs/` để tạo prefab, rồi xoá object trong Scene.

### 6.3 Vẽ hitbox theo frame

1. Double-click prefab để mở Prefab Mode.
2. Trong Inspector của Track, bấm **Open Animation Hitbox Editor**. Cũng có thể mở bằng `Tools > Animation Hitbox Editor`.
3. Bấm **Sync Frames From Animation Clip** để tạo danh sách frame.
4. Chọn frame trên timeline bằng các nút `|<`, `<`, `>`, `>|`, thanh trượt hoặc ô số. Frame có hitbox được đánh dấu `●`.
5. Ở danh sách hitbox, bấm `+` để thêm box. Kéo trực tiếp trên ảnh preview hoặc trong Scene View để di chuyển và đổi kích thước.
6. **Copy Previous Frame** sao chép box từ frame trước, **Clear Frame** xoá toàn bộ box của frame.
7. Bấm **Play** để xem lại, rồi lưu prefab.

Lúc chạy, mỗi kẻ địch chỉ nhận sát thương một lần trong một đòn đánh, kể cả khi chạm nhiều box hoặc nhiều frame.

### 6.4 Tạo asset Attack

`Create > Weapon > Attack Definition`, lưu tại `Melee/Attacks/`:

- **Lifetime = Transient**.
- **Attack Prefab** = prefab vừa tạo.
- **Animator State Name** = tên state, ví dụ `Slash`.
- **Hit Limit = -1**.

## 7. Vùng sát thương quanh nhân vật (Aura)

### 7.1 Tạo Attack Prefab

1. `Create Empty`, đặt tên ví dụ `MyAuraAttackActor`.
2. **Add Component** `Weapon Aura Actor`. Unity tự thêm `Circle Collider 2D`.
3. Trong `Circle Collider 2D`: bật **Is Trigger**, đặt **Radius** là bán kính gây sát thương.
4. Tạo object con cho hình ảnh và thêm `Sprite Renderer`. Chỉnh kích thước sao cho khớp với vòng tròn collider.
5. Trong `Weapon Aura Actor`: kéo collider vào **Detection Area**, kéo object con vào **Visual Root**.
6. Lưu prefab vào `GameContent/Weapons/Aura/Prefabs/`.

Area Multiplier scale toàn bộ prefab, nên vùng gây sát thương và hình ảnh luôn to lên cùng nhau.

**Damage Multiplier** trên `Weapon Aura Actor` là hệ số damage của mỗi hit. Muốn Aura mạnh hơn riêng theo level, tạo một prefab khác có hệ số lớn hơn và dùng trong Attack của level đó.

### 7.2 Tạo asset Attack

`Create > Weapon > Attack Definition`, lưu tại `Aura/Attacks/`:

- **Lifetime = Persistent**. Đây là điểm bắt buộc của Aura.
- **Attack Prefab** = prefab vừa tạo.
- **Hit Cooldown** = số giây giữa hai lần gây sát thương, ví dụ `0.5`.
- **Hit Limit** = số mục tiêu tối đa mỗi nhịp, `-1` là không giới hạn.

Với Aura, **Attack Interval** không được dùng và **Haste không rút ngắn Hit Cooldown**.

## 8. Vũ khí bắn đạn (Projectile)

Projectile có 3 lớp:

```text
ProjectileWeaponAttackDefinition   Bắn thế nào: mấy viên, kiểu loạt, hệ số damage
  ├─ Attack Prefab: ProjectileAttackActor.prefab   Prefab điều phối, tạo một lần rồi dùng chung
  └─ ProjectileDefinition          Đạn bay và va chạm thế nào
       └─ Projectile Prefab        Hình ảnh viên đạn
```

### 8.1 Tạo Attack Prefab

Prefab này chỉ tạo đạn và tự huỷ khi mọi viên đã xong. Project đã có sẵn `Ranged/Projectile/Prefabs/ProjectileAttackActor.prefab` dùng chung cho mọi vũ khí đạn, nên chỉ cần làm các bước dưới đây nếu prefab đó bị mất.

1. `Create Empty`, đặt tên `ProjectileAttackActor`.
2. **Add Component** `Projectile Weapon Attack Actor`. Component này không có trường nào cần điền.
3. Không thêm hình ảnh hay collider vào prefab này.
4. Lưu prefab vào `Ranged/Projectile/Prefabs/`.

### 8.2 Tạo prefab viên đạn

1. `Create Empty`, đặt tên ví dụ `MyBulletProjectile`.
2. **Add Component** `Weapon Projectile Actor`. Component này không có trường nào cần điền.
3. Thêm `Sprite Renderer` trên chính object hoặc object con, có thể thêm `Trail Renderer`.
4. **Hướng vẽ:** đầu viên đạn phải hướng theo trục `+X`, tức là sang phải.
5. **Không cần** `Rigidbody2D` hay `Collider2D`, vì đạn tự quét va chạm bằng Raycast hoặc CircleCast.
6. Lưu prefab vào `Ranged/Projectile/Prefabs/`.

### 8.3 ProjectileDefinition

**Tạo:** `Create > Weapon > Projectile > Definition`, lưu tại `Ranged/Projectile/Definitions/`.

**Cơ bản**

| Trường | Ý nghĩa |
|---|---|
| Projectile Prefab | Prefab viên đạn ở mục 8.2 |
| Speed | Tốc độ bay, đơn vị/giây |
| Lifetime | Thời gian sống tối đa, giây |
| Maximum Range | Quãng đường tối đa, đồng thời giới hạn điểm đến theo chuột |
| Collision Radius | Bán kính va chạm. `0` là tia mảnh |
| Obstacle Layer Mask | Layer chặn đạn, ví dụ `Ostacle`. **Không chọn `Enemy`**. Để `Nothing` thì đạn bay xuyên vật cản |

**Đường bay**

| Trường | Ý nghĩa |
|---|---|
| Trajectory | `Straight` bay thẳng. `Parabolic` bay cong tới điểm chuột lúc bắn |
| Randomize Straight Or Curve, Straight Path Chance | Mỗi viên Parabolic có xác suất chuyển thành đường thẳng |
| Arc Height | Độ cong |
| Randomize Arc Height, Minimum/Maximum Arc Height | Mỗi viên chọn độ cong ngẫu nhiên trong khoảng |
| Arc Side | Phía cong: `Left`, `Right`, `Alternate` (xen kẽ), `Random` |
| Minimum Aim Distance | Khoảng cách tối thiểu tới điểm đến khi chuột ở sát nhân vật |

Mỗi viên chốt đường bay một lần lúc xuất hiện và không đổi trong khi bay.

**Impact Mode**: chọn một chế độ, chỉ điền nhóm trường của chế độ đó.

| Chế độ | Hành vi | Trường sử dụng |
|---|---|---|
| `PierceAll` | Xuyên qua kẻ địch | Maximum Pierced Targets (`-1` không giới hạn), Damage Multiplier Per Pierce |
| `TargetBounce` | Nảy sang kẻ địch gần nhất | Bounce Radius, Maximum Bounces, Damage Multiplier Per Bounce, Prevent Repeat Targets |
| `ExplodeOnFirstEnemy` | Nổ khi chạm kẻ địch đầu tiên | Explosion Radius, Explosion Damage Multiplier, Maximum Explosion Targets |
| `ExplodeAtEnd` | Bay qua kẻ địch, nổ tại điểm đến hoặc khi chạm vật cản | Giống `ExplodeOnFirstEnemy` |

`ExplodeOnFirstEnemy` chỉ nổ khi chạm kẻ địch. Đạn chạm vật cản hoặc bay hết tầm thì biến mất mà không nổ.

**VFX**

| Trường | Ý nghĩa |
|---|---|
| Impact Vfx Prefab | Hiệu ứng khi đạn trúng trực tiếp hoặc chạm vật cản |
| Explosion Vfx Prefab | Hiệu ứng khi nổ |
| Vfx Lifetime | Thời gian tự huỷ của hiệu ứng |
| Ground Zone Prefab | Vùng sát thương để lại nơi đạn nổ, dùng với `ExplodeOnFirstEnemy` hoặc `ExplodeAtEnd` (mục 22.3) |

### 8.4 ProjectileWeaponAttackDefinition

**Tạo:** `Create > Weapon > Attack > Projectile`, lưu tại `Ranged/Projectile/Attacks/`.

Đặt **Lifetime = Transient** và **Attack Prefab** = prefab ở mục 8.1. Không kéo prefab viên đạn vào đây.

| Trường | Ý nghĩa |
|---|---|
| Projectile | ProjectileDefinition ở mục 8.3 |
| Shot Mode | `Shot` 1 viên, `Multishot` nhiều viên cùng lúc, `Burst` nhiều viên liên tiếp, `BurstSequence` lặp lại nhiều loạt Burst |
| Spawn Pattern | `Forward` cùng hướng, `Cone` toả đều trong góc, `RandomLanding` bắn về phía chuột với điểm rơi ngẫu nhiên |
| Landing Spread Radius | Bán kính vùng điểm rơi quanh chuột, chỉ dùng với `RandomLanding` |
| Projectile Count | Số viên, hoặc số viên mỗi loạt với BurstSequence. `Shot` bỏ qua trường này |
| Cone Angle | Tổng góc toả, chỉ dùng với `Cone` |
| Burst Shot Interval | Giãn cách giữa các viên trong một loạt, Haste rút ngắn |
| Burst Repeat Count | Tổng số loạt, chỉ dùng với BurstSequence |
| Burst Repeat Interval | Thời gian nghỉ giữa các loạt, Haste rút ngắn |
| Per Projectile Damage Multiplier | Hệ số damage mỗi viên. Với 5 viên, `0.2` cho tổng bằng một hit nếu trúng cả 5 |
| Hit Policy | `OncePerProjectile`: mỗi viên có thể trúng cùng một kẻ địch. `OncePerVolley`: cả loạt chỉ trúng mỗi kẻ địch một lần |

Hướng ngắm được khoá khi bắt đầu đòn đánh. Cooldown tính từ lúc bắt đầu, nên hãy đặt **Attack Interval** dài hơn thời gian bắn hết loạt nếu không muốn các loạt chồng lên nhau.

```text
Burst         = (Projectile Count - 1) × Burst Shot Interval
BurstSequence = Repeat Count × (Projectile Count - 1) × Shot Interval + (Repeat Count - 1) × Repeat Interval
```

## 9. Vũ khí tia (Beam)

### 9.1 Tạo Attack Prefab

1. `Create Empty`, đặt tên `BeamAttackActor`.
2. **Add Component** `Beam Weapon Attack Actor`. Unity tự thêm `Line Renderer`.
3. Kéo Line Renderer vào trường **Line Renderer** của actor.
4. Trong Line Renderer, chỉnh **Sorting Layer** và **Order in Layer**. Độ rộng và màu sẽ bị ghi đè lúc chạy.
5. Lưu prefab vào `GameContent/Weapons/Ranged/Beam/Prefabs/`. Có thể tạo folder bằng `Create > Folder`.

### 9.2 Tạo Material cho tia

1. `Create > Material`, đặt Shader là `Universal Render Pipeline/2D/Sprite-Unlit-Default`.
2. Lưu vào `Ranged/Beam/Visuals/`.

Không có Material thì tia sẽ hiện màu hồng hoặc không hiện.

### 9.3 BeamWeaponAttackDefinition

**Tạo:** `Create > Weapon > Attack > Beam`, lưu tại `Ranged/Beam/Attacks/`. Đặt **Lifetime = Transient** và **Attack Prefab** = prefab ở trên.

| Trường | Ý nghĩa |
|---|---|
| Beam Mode | `Pulse` gây sát thương một lần. `Continuous` gây sát thương theo nhịp |
| Aim Tracking | `SnapshotOnAttack` khoá hướng lúc bắn. `FollowCursor` đi theo chuột |
| Maximum Range | Chiều dài tia |
| Beam Width | Độ dày vùng gây sát thương. `0` là tia mảnh |
| Duration | Thời gian tồn tại của tia |
| Damage Tick Interval | Nhịp gây sát thương, chỉ dùng với `Continuous` |
| Damage Multiplier Per Tick | Hệ số damage mỗi nhịp |
| Hit Mode | `FirstTarget`, `PierceAll` hoặc `LimitedTargets` |
| Maximum Targets | Số mục tiêu tối đa, chỉ dùng với `LimitedTargets` |
| On Hit Policy | `EveryDamageTick` kích hoạt OnHit mỗi nhịp. `OncePerActivation` chỉ kích hoạt một lần mỗi kẻ địch trong một lần chiếu |
| Obstacle Layer Mask | Layer làm tia dừng lại |
| Start Width, End Width | Độ rộng hình ảnh, độc lập với Beam Width |
| Start Color, End Color | Màu hai đầu tia |
| Beam Material | Material ở mục 9.2 |
| Muzzle Vfx Prefab | Hiệu ứng ở đầu tia |
| Impact Vfx Prefab | Hiệu ứng ở cuối tia, di chuyển theo điểm cuối |

Trường **Impact Vfx Lifetime** hiện không có tác dụng, vì hiệu ứng được huỷ cùng tia.

Với tia `Continuous` có gây status, nên chọn `OncePerActivation` để không chồng stack quá nhanh.

## 10. Gắn vũ khí vào nhân vật

Vũ khí có sẵn từ đầu trận được gắn vào prefab nhân vật. Vũ khí nhận qua nâng cấp thì xem mục 14.

1. Mở `GameContent/Player/Iris/Iris.prefab` bằng cách double-click.
2. Chuột phải vào object con **WeaponController** rồi chọn `Create Empty`. Đặt tên theo vũ khí.
3. **Add Component** `Weapon Controller` trên object mới.
4. Điền các trường bên dưới rồi lưu prefab.

| Trường | Ý nghĩa | Thông thường |
|---|---|---|
| Weapon Data | Asset vũ khí | **Bắt buộc** |
| Starting Level | Level bắt đầu, tính từ 0 | `0` |
| Combat Stats | Nguồn chỉ số | Để trống, tự tìm `CharacterCombatSystem` ở object cha |
| Attack Origin | Điểm xuất phát đòn đánh | Để trống, dùng chính object này |
| Aim Provider Behaviour | Nguồn hướng ngắm | Để trống, tự tìm `LookAtMouse` ở object cha |
| Auto Attack | Tự đánh khi hết cooldown | Bật |
| Attack Immediately | Đánh ngay khi bắt đầu | Tuỳ ý |

Phải đặt vũ khí **bên trong** object `WeaponController` của Iris, vì `LookAtMouse` xoay object này theo chuột và `CharacterCombatSystem` nằm trên đó.

Vũ khí gắn sẵn **tính vào số slot** `Maximum Weapon Slots` của bảng nâng cấp.

Vũ khí gắn sẵn **vẫn phải có trong Weapons** của `LevelUpDraftConfig` (mục 14), nếu không bảng nâng cấp sẽ không hiện thẻ nâng cấp và thẻ dạng tối thượng của nó.

## 11. Triggered Effects: OnAttack, OnHit, OnKill

Thêm trong `WeaponData > Levels > Element N > Triggered Effects`, bấm `+`:

| Trường | Ý nghĩa |
|---|---|
| Trigger | `OnAttack` mỗi lần đánh. `OnHit` mỗi hit được chấp nhận. `OnKill` khi hit đó giết mục tiêu |
| Trigger Chance | `1` là 100%, `0.25` là 25% |
| Internal Cooldown | Cooldown riêng của dòng này trên vũ khí, giây |
| Allow Secondary Hits | Cho phép kích hoạt từ hit phụ như status, mark hay hit do effect tạo ra. Nên để tắt, trừ khi muốn phản ứng dây chuyền: dòng OnKill của dạng Explosive Slash bật trường này để vụ nổ giết được kẻ địch thì nổ tiếp |
| Effect | Asset effect (mục 12) |
| Effect Prefab | Prefab hiệu ứng (bên dưới). Chỉ dùng khi **Effect** trống |
| Generated Hits Can Trigger On Hit | Cho hit do effect tạo ra kích hoạt OnHit tiếp. Bật có thể tạo chuỗi, code giới hạn ở độ sâu 4 |
| Damage Multiplier | Hệ số damage cho Effect Prefab |

`OnAttack` chưa có mục tiêu, nên chỉ dùng được với Effect Prefab, không dùng được với Apply Status, Direct Damage hay Knockback.

### Tạo Effect Prefab

Prefab phải có component kế thừa `WeaponTriggeredEffectActor` ngay trên object gốc. Hiện có 2 loại.

**Đạn phụ `SlashProjectileEffectActor`** bay thẳng và xuyên qua kẻ địch:

1. `Create Empty`, thêm `Sprite Renderer` với hình hướng theo `+X`.
2. **Add Component** `Slash Projectile Effect Actor`. Unity tự thêm `Rigidbody 2D`, nhưng `Collider 2D` là lớp trừu tượng nên bạn **thêm trước** một collider cụ thể như `Capsule Collider 2D` và bật **Is Trigger**.
3. Trong `Rigidbody 2D`: **Body Type = Kinematic**, **Gravity Scale = 0**.
4. Điền **Speed**, **Lifetime**, kéo Rigidbody vào **Body**.

**Vụ nổ `DeathExplosionEffectActor`** gây sát thương một lần trong vùng tròn:

1. `Create Empty`, thêm hình ảnh hoặc animation.
2. **Add Component** `Death Explosion Effect Actor`. Unity tự thêm `Circle Collider 2D`.
3. Đặt **Radius** của collider là bán kính nổ.
4. Kéo collider vào **Damage Area**, đặt **Visual Lifetime** là thời gian tồn tại.

Lưu tại `GameContent/Weapons/<Loại>/Prefabs/`, rồi kéo vào trường **Effect Prefab**. Damage được tính bằng công thức của vũ khí nhân với **Damage Multiplier**.

## 12. Status effect và effect asset

Muốn gây Burn cho kẻ địch cần 3 tầng:

```text
Triggered Effect (Trigger = OnHit)
  └─ Effect: ApplyBurnEffect       Asset Apply Status
       └─ Status: BurnStatus       Asset định nghĩa Burn
```

Project đã có `BurnStatus` và `ApplyBurnEffect` (mục 22.1), dùng lại được ngay. Effect và status mới thì tạo trong `GameContent/Weapons/Effects/`:

| Thư mục | Chứa |
|---|---|
| `Statuses/` | Asset Status |
| `Applications/` | Asset Apply Status |
| `Direct/` | Asset Direct Damage, Knockback, Composite. Folder này chưa có, tạo khi cần |

**Burn đã được tạo theo cách sau, làm tương tự với status khác:**

1. Trong `Statuses/`, chọn `Create > Weapon > Status > Damage Over Time`, đặt tên `BurnStatus` và điền các trường ở mục 12.2.
2. Trong `Applications/`, chọn `Create > Weapon > Effect > Apply Status`, đặt tên `ApplyBurnEffect` và kéo `BurnStatus` vào **Status**.
3. Trong WeaponData, thêm một dòng Triggered Effects, đặt **Trigger = OnHit** và kéo `ApplyBurnEffect` vào **Effect**.

**Giá trị tham khảo để bắt đầu:**

| Effect | Loại | Cấu hình |
|---|---|---|
| Burn | Damage Over Time | 4 giây, 5 stack, AddStackAndRefresh, Flat 2, Source 0.1, Tick 1, Fire |
| Poison | Damage Over Time | 6 giây, 3 stack, AddStackAndRefresh, SeparatePerSource, Flat 1, Source 0.08, Tick 0.75, Poison |
| Slow | Slow | 2 giây, RefreshDuration, Multiplier 0.7 |
| Stun | Stun | 0.6 giây, ExtendDuration |
| Mark | Mark | 6 giây, 3 stack, AddStackAndRefresh, nổ ở 3 stack bằng một Direct Damage |
| Mark Burst | Direct Damage | Flat 5, Source 0.8, Arcane |
| Knockback | Knockback | Strength 5 |

### 12.1 Asset Effect

| Menu Create | Trường | Ý nghĩa |
|---|---|---|
| `Weapon > Effect > Apply Status` | Status | Status áp lên mục tiêu |
| `Weapon > Effect > Direct Damage` | Flat Damage, Source Damage Multiplier, Element, Can Trigger On Hit | Damage = Flat + damage của hit gốc × Multiplier |
| `Weapon > Effect > Knockback` | Strength | Đẩy mục tiêu ra xa người chơi |
| `Weapon > Effect > Composite` | Effects | Chạy nhiều effect cùng lúc, ví dụ Burn và Knockback |

### 12.2 Asset Status

Trường chung của mọi status:

| Trường | Ý nghĩa |
|---|---|
| Status Id | ID, nếu trống thì dùng tên asset |
| Duration | Thời gian tồn tại |
| Maximum Stacks | Số stack tối đa |
| Stack Policy | `RefreshDuration` làm mới thời gian. `AddStackAndRefresh` thêm stack và làm mới. `ExtendDuration` cộng thêm thời gian. `IndependentInstances` mỗi lần áp là một bản riêng |
| Stack Scope | `SharedAcrossSources` dùng chung stack. `SeparatePerSource` tách stack theo người gây |
| Visual Style | Kiểu particle: `None`, `Mark`, `Slow`, `Stun`, `Burn`, `Poison` |
| Visual Color, Visual Offset, Visual Scale | Màu, vị trí và kích thước particle, không ảnh hưởng gameplay |

Trường riêng:

| Menu Create | Trường | Ý nghĩa |
|---|---|---|
| `Weapon > Status > Damage Over Time` | Flat Damage, Source Damage Multiplier, Tick Interval, Tick Immediately, Element | Damage mỗi nhịp = (Flat + damage hit gốc × Multiplier) × số stack. Không chí mạng, không kích hoạt OnHit |
| `Weapon > Status > Slow` | Movement Speed Multiplier | `0.7` còn 70% tốc độ. Nhiều slow thì lấy giá trị nhỏ nhất |
| `Weapon > Status > Stun` | Không có | Đứng yên trong thời gian tồn tại |
| `Weapon > Status > Mark` | Reactions | Phản ứng theo stack, xem bên dưới |

Mỗi **Reaction** của Mark:

| Trường | Ý nghĩa |
|---|---|
| Trigger | `OnApplied`, `OnStackChanged`, `OnThresholdReached`, `OnExpired` |
| Required Stacks | Số stack cần có |
| Consume Stacks | Số stack tiêu thụ sau khi chạy |
| Effects | Các effect sẽ chạy |

Ví dụ với **Trigger = OnThresholdReached**, **Required Stacks = 3**, **Consume Stacks = 3**: khi đạt 3 stack, mục tiêu chịu các effect trong **Effects** rồi mất 3 stack.

### 12.3 Yêu cầu trên kẻ địch

| Tính năng | Enemy cần có | Prefab Enemy hiện tại |
|---|---|---|
| Nhận sát thương | `Collider2D` trên layer `Enemy` và component `IDamageReceiver` | ✓ (`EnemyHealth`) |
| Nhận status | `StatusEffectController` cùng object với `EnemyHealth` | ✓ |
| Thấy particle status | `StatusEffectVisualController` | ✓ |
| Slow, Stun, Knockback | `EnemyFlowFieldAgent` | ✓ |

## 13. Bonus chỉ số và luật chỉ số

### Stat Bonuses

Mỗi level cấp thêm bonus cho **nhân vật**, không chỉ cho vũ khí đó. Bonus cộng dồn từ level 1 đến level hiện tại. Giá trị tính theo **phần trăm của chỉ số gốc**: `30` nghĩa là +30% chỉ số gốc, không tính trên tổng hiện tại. Riêng Critical Chance cộng thẳng điểm phần trăm, và Max Health cộng thẳng số máu (mục 15).

| Stat | Tác dụng |
|---|---|
| Attack Power Percent | Tăng damage |
| Critical Chance Percent | Tăng tỉ lệ chí mạng |
| Attack Speed Percent | Haste: giảm Attack Interval, tăng tốc animation và burst |
| Movement Speed Percent | Tăng tốc độ di chuyển |
| Weapon Area Percent | Tăng kích thước **mọi** vũ khí |
| Experience Pickup Range Percent | Tăng tầm nhặt EXP |
| Max Health | Cộng thẳng số máu tối đa, ví dụ `25` là +25 máu. Dùng được ở level vũ khí, dạng tiến hoá, item, gói chỉ số và Ultimate |

### Stat Restrictions

Luật này chặn hoặc chuyển đổi chỉ số đến từ nguồn khác:

| Trường | Ý nghĩa |
|---|---|
| Stat | Chỉ số bị ảnh hưởng |
| Affected Sources | Nguồn bị ảnh hưởng: BaseStats, InspectorBonus, WeaponLevel, Equipment, Passive, TemporaryBuff |
| Mode | `Block` bỏ giá trị. `Convert` đổi sang chỉ số khác |
| Converted Stat, Conversion Ratio | Chỉ số đích và tỉ lệ đổi, chỉ dùng với `Convert`. Giá trị sau khi đổi được tính như một bonus của chỉ số đích (mục 15) |
| Priority | Số lớn thắng khi nhiều luật cùng áp dụng |
| Allow Owning Source | Cho phép chính vũ khí sở hữu luật vẫn nhận chỉ số đó |

## 14. Đưa vũ khí vào bảng nâng cấp

1. Chọn `Assets/Resources/LevelUpDraftConfig`.
2. Ở danh sách **Weapons**, bấm `+`.
3. Kéo WeaponData vào **Weapon Data**, để trống **Acquisition Prefab**.
4. Đặt **Offer Weight** là tỉ lệ xuất hiện và **Minimum Player Level** là level nhân vật tối thiểu.
5. Kiểm tra **Maximum Weapon Slots**. Vũ khí gắn sẵn trên prefab cũng chiếm slot.

Thẻ nâng cấp lấy **Display Name**, **Icon** và **Description** của level. Vũ khí đã đạt level cuối sẽ không còn được đề xuất.

Nếu level 1 của vũ khí là Aura, hãy dùng **Acquisition Prefab** (mục 18). Prefab đó có `WeaponController` với **Weapon Data** đã gán, lưu tại `GameContent/Weapons/Aura/Prefabs/`.

## 15. Công thức tính

**Sát thương**

```text
Damage = Base Damage × (ATK / 100) × Hệ số phụ
Damage = Damage × ngẫu nhiên(1 - Variance/100, 1 + Variance/100)
Nếu chí mạng: Damage = Damage × Critical Hit Multiplier
Làm tròn thành số nguyên
```

Hệ số phụ là `Per Projectile Damage Multiplier`, `Explosion Damage Multiplier`, `Damage Multiplier Per Tick`, `Damage Multiplier` của Triggered Effect, hoặc hệ số bounce, pierce tuỳ trường hợp.

Ví dụ với ATK = 150, Critical Hit Multiplier = 1.75, Variance = 0:

- Base Damage 100: `100 × 1.5 = 150`, chí mạng `150 × 1.75 = 262`.
- Base Damage 10, Per Projectile 0.4: `10 × 1.5 × 0.4 = 6`, chí mạng `6 × 1.75 = 10`.

**Tốc độ đánh** (khi bật Affected By Haste)

```text
Interval = Attack Interval / (1 + Haste / 100)
Interval = max(Interval, Minimum Attack Interval)
Tốc độ animation = 1 + Haste / 100
```

**Bonus chỉ số**

```text
ATK, SPD, PUR, Area = Gốc + Gốc × (tổng bonus / 100)
Haste               = Gốc + (100 + Gốc) × (tổng bonus / 100)
CRT                 = Gốc + tổng bonus
Máu tối đa          = HP gốc + tổng bonus (cộng theo số)
```

Weapon Area gốc của nhân vật là 100. Mọi nguồn bonus đều tính trên chỉ số gốc nên không nhân chồng lên nhau.

**Kích thước**

```text
Hệ số Area = WeaponData.Area Multiplier × (Weapon Area / 100)
```

Hệ số này áp cho scale của prefab, hitbox melee, vùng aura, bán kính va chạm đạn, vụ nổ, bán kính nảy và độ rộng tia.

## 16. Kiểm tra và debug

**Trước khi bấm Play**

- Đặt **Damage Variance Percent = 0** để damage dễ đoán.
- Prefab Enemy có **Log Accepted Hits** đang bật, nên Console in từng hit dạng `[DAMAGE] Target=... | Damage=... | HP=...`.

**Công cụ khi chạy**

| Ở đâu | Công cụ | Dùng để |
|---|---|---|
| Inspector của `Weapon Controller` | Nút **Debug Level Up** và mục Runtime Level Values | Tăng level ngay, xem cooldown và ATK, CRT, Haste, Area thực tế |
| Inspector của `Level Up Draft Controller` | Nút **Open Draft For Current Level** | Mở bảng nâng cấp ngay |
| Inspector của `Player Experience` | Nút **+1 EXP**, **+10 EXP**, **+100 EXP** | Lên level nhanh |
| Scene View | Khung đỏ của hitbox khi `Show Hitboxes In Scene` bật | Xem hitbox melee đang hoạt động |
| Object `FlowFieldSystem` | `Flow Field Debug` | Xem hướng di chuyển của enemy |

**Checklist khi vũ khí không hoạt động**

1. Console có lỗi không?
2. Object vũ khí có đang active và `Weapon Controller` có đang bật không?
3. **Remaining Cooldown** có giảm và về 0 không?
4. Level hiện tại có **Attack**, và Attack có **Attack Prefab** không?
5. **Target Layer Mask** có `Enemy` không?
6. **Area Multiplier** có khác 0 không?

## 17. Lỗi thường gặp

| Hiện tượng | Nguyên nhân | Cách sửa |
|---|---|---|
| Không có đòn đánh nào | Attack hoặc Attack Prefab trống | Gán đủ |
| Console báo `chưa được gán WeaponData` | WeaponController thiếu dữ liệu | Gán **Weapon Data** |
| Console báo `Weapon ... chưa có level` | Levels rỗng | Thêm level |
| Có animation nhưng không gây sát thương | Sai Clip, chưa có hitbox trong frame, hoặc sai Target Layer Mask | Mở Hitbox Editor, kiểm tra Sync Frames |
| Animation không chạy | Animator State Name sai | Sửa cho khớp tuyệt đối với state |
| Hitbox lệch hướng | Sprite không vẽ theo `+Y` | Vẽ lại hitbox hoặc xoay sprite |
| Đạn bay ngang so với hình | Sprite đạn không hướng `+X` | Xoay object con của sprite |
| Đạn không trúng | Target Layer Mask sai hoặc Collision Radius quá nhỏ | Kiểm tra lại mask, tăng bán kính |
| Đạn dừng khi trúng kẻ địch | `Enemy` nằm trong Obstacle Layer Mask | Bỏ `Enemy` khỏi mask |
| Console báo `requires a ProjectileWeaponAttackDefinition` | Asset Attack sai loại hoặc thiếu Projectile Prefab | Tạo đúng loại, gán đủ |
| Tia màu hồng | Thiếu Material | Gán **Beam Material** |
| Aura không gây sát thương | Lifetime đang là Transient | Đặt **Persistent** |
| Aura không nhanh hơn khi có Haste | Hit Cooldown không chịu Haste | Giảm Hit Cooldown ở level sau |
| Không thấy status | Effect chưa trỏ tới Status, hoặc Trigger Chance bằng 0 | Kiểm tra asset |
| Console báo `Triggered Effect prefab ... must have a WeaponTriggeredEffectActor` | Đã kéo prefab đòn đánh vào **Effect Prefab** | Dùng prefab hiệu ứng theo mục 11, không dùng Attack Prefab |
| Hiệu ứng level 2 mất ở level 3 | Triggered Effects không kế thừa | Thêm lại ở level 3 |
| `OnAttack` với Apply Status không có tác dụng | OnAttack chưa có mục tiêu | Dùng OnHit |
| Hiệu ứng kích hoạt quá nhiều | Nhiều viên đạn hoặc tia nhiều nhịp | Tăng Internal Cooldown, dùng OncePerVolley hoặc OncePerActivation |
| Vũ khí mới không xuất hiện khi lên cấp | Hết slot, chưa đủ level, hoặc chưa có trong config | Xem mục 14 |
| Aura có vòng rất nhỏ và không trúng ai | Level 1 là Aura nhưng thiếu Acquisition Prefab | Gán **Acquisition Prefab** (mục 18) |
| Đạn nổ không xảy ra khi bay hết tầm | `ExplodeOnFirstEnemy` chỉ nổ khi chạm kẻ địch | Dùng `ExplodeAtEnd` |
| Ultimate không gây sát thương | Chưa gán **Attack Weapon**, hoặc Attack là Persistent mà **Duration** bằng 0 | Xem mục 23 |

## 18. Giới hạn hiện tại của code

Những điểm dưới đây là hành vi thực tế của code, nên tính đến khi cấu hình:

- **Hit Rule không có tác dụng.** Melee luôn đánh mỗi mục tiêu một lần trong một đòn. Hit Cooldown chỉ dùng cho Aura.
- **Element chưa ảnh hưởng gameplay.** Giá trị được ghi vào damage nhưng chưa có kháng nguyên tố, và damage chính của vũ khí luôn là `Physical`.
- **Aura ở level 1 khi nhận qua bảng nâng cấp mà không có Acquisition Prefab sẽ có bán kính gần 0**, do actor được khởi tạo trước `Awake`. Hãy dùng Acquisition Prefab. Đổi sang Aura từ level 2 trở đi thì không bị.
- **Aura ở level 1 gắn sẵn trên nhân vật có thể gặp cùng lỗi**, vì đi qua cùng luồng khởi tạo. Điểm này suy ra từ code và chưa được kiểm chứng trong game, nên hãy kiểm tra bán kính khi chạy thử.
- **`TargetBounce`:** `Prevent Repeat Targets = false` không cho phép đánh lại mục tiêu cũ, và một lần va chạm lặp với mục tiêu cũ có thể tiêu mất một lượt nảy.
- **`PlayerHealth`:** Máu tối đa lấy từ HP trong `CharacterStats` cộng bonus Max Health. Khi hết máu chưa có xử lý thua cuộc.

## 19. Công thức cấu hình mẫu

| Vũ khí | Cấu hình |
|---|---|
| **Súng xuyên** | Straight, PierceAll, Pierced `-1`. Shot, Forward, hệ số 1 |
| **Shotgun** | Straight, PierceAll, Pierced `1`. Multishot, Cone, 5 viên, 40°, hệ số 0.3, OncePerVolley |
| **Đạn nảy** | Straight, TargetBounce, Radius 4, Bounces 3, hệ số nảy 0.8. Shot, Forward |
| **Rocket cong** | Parabolic, Arc Height 2.5, Arc Side Random, ExplodeOnFirstEnemy, Radius 2.5. Shot, Forward |
| **Bom rơi tại chuột** | Parabolic, ExplodeAtEnd, Radius 2.5. Burst, 2 viên, Forward, hệ số 0.4 |
| **Súng máy** | Straight, PierceAll, Pierced `1`. BurstSequence, 3 viên, 3 loạt, Shot 0.1, Repeat 0.4 |
| **Tia một phát** | Beam Pulse, Snapshot, Duration 0.15, Range 14, PierceAll |
| **Tia liên tục** | Beam Continuous, FollowCursor, Duration 2, Tick 0.2, hệ số 0.2, OncePerActivation |
| **Vùng lửa** | Aura, Persistent, Hit Cooldown 0.5. Thêm OnHit một effect Apply Status gây Burn, Internal Cooldown 0.1 |
| **Chém làm chậm** | Melee. Thêm OnHit một effect Apply Status gây Slow, Chance 1 |

Khi dùng Pierced `1`, đạn biến mất sau mục tiêu đầu tiên.

## 20. Vũ khí đang có trong project

Cấu hình cũ đã được xoá để làm lại từ đầu. Hiện project có 2 vũ khí hoàn chỉnh, cả hai đều nằm trong bảng nâng cấp:

| Vũ khí | File | Cách nhận | Cấu hình chính |
|---|---|---|---|
| Slash | `GameContent/Player/Iris/MainWeapon/SlashData.asset` | Melee, gắn sẵn trên Iris | Base Damage `50`, Attack Interval `1`, 6 level thường, 2 dạng: Projectile Slash và Explosive Slash |
| Fire Zone | `GameContent/Weapons/Aura/Weapons/FireZone.asset` | Aura, nhận qua bảng nâng cấp | Base Damage `10`, 3 level thường, 2 dạng (mục 22) |

Trạng thái các thành phần liên quan:

| Thành phần | Trạng thái hiện tại |
|---|---|
| `Iris.prefab` | Có sẵn Slash, Ultimate là `IrisUltimate` (mục 23). Thêm vũ khí gắn sẵn theo mục 10 |
| `Lily.prefab`, `Zena.prefab` | Chưa gắn vũ khí nào |
| `LevelUpDraftConfig` | **Weapons** gồm Slash và Fire Zone, **Maximum Weapon Slots = 2**, **Maximum Item Slots = 0** |
| `GameContent/Weapons/` | Có đủ content của Fire Zone, `ProjectileAttackActor`, `BurnStatus` và `ApplyBurnEffect`. Các folder trong `Melee/` đang rỗng, `Ranged/Beam/` chưa có |
| Effect của Iris | `GameContent/Player/Iris/MainWeapon/Effects/KnockbackEffect.asset`, dùng cho Explosive Slash |

**Hình ảnh và animation vẫn còn để dùng lại:**

| Đường dẫn trong `GameContent/Weapons/` | Nội dung |
|---|---|
| `../Player/Iris/MainWeapon/Visuals/` | `sprite-sheet.png`, `Slash.anim`, `Slash.controller` (state `Slash`) |
| `Aura/Visuals/` | `flamezone.png` |
| `Ranged/Projectile/Visuals/Explosion/` | `explosion.png`, `Explosion.anim`, `ExplosionVisual.controller` |
| `Ranged/Projectile/Visuals/` | `TripleArcBombTrail.mat` |

Cấu hình cũ vẫn lưu trong git. Muốn xem hoặc lấy lại một file, dùng commit `919fb2e`, ví dụ:

```bash
git checkout 919fb2e -- "Assets/GameContent/Weapons/Melee/Prefabs/SlashAttackActor.prefab"
```

## 21. Dạng tối thượng của level cuối

Vũ khí có hai phần tách biệt:

| Phần trong Inspector | Nội dung |
|---|---|
| **Normal Levels** | Các level thường, mỗi level là một đòn cấu hình đầy đủ |
| **Level N (Max Level): Ultimate Forms** | Level tối đa, **chỉ** là danh sách các dạng. Không có cấu hình đòn thường |

Ví dụ có 3 level thường và 2 dạng:

```text
Level 1 → Level 2 → Level 3 → Level 4 (Max): chọn Dạng A hoặc Dạng B
```

Level tối đa luôn đứng sau level thường cuối cùng. Thêm một phần tử vào **Normal Levels** thì danh sách dạng tự nhảy xuống: 3 level thường thành 4, level tối đa thành Level 5. Xoá một level thường thì nó nhảy lên lại. Danh sách dạng không bị ảnh hưởng.

**Forms** trống nghĩa là vũ khí không có dạng tối thượng, khi đó level thường cuối cùng là level tối đa.

### 21.1 Dạng tối thượng lấy gì từ các level trước

| Phần | Kết quả sau khi chọn dạng |
|---|---|
| **Stat Bonuses**, **Stat Restrictions** | Giữ của mọi level thường, cộng thêm của dạng |
| **Attack** | Dạng có Attack thì dùng của dạng. Để trống thì dùng Attack của level thường cuối cùng |
| **Triggered Effects** | Chỉ dùng của dạng, không kế thừa như mọi level khác |

### 21.2 Tạo một dạng tối thượng

1. Chọn asset WeaponData.
2. Đến mục **Level N (Max Level): Ultimate Forms** ngay dưới **Normal Levels**.
3. Ở danh sách **Forms**, bấm `+` và điền các trường bên dưới. Nếu còn thiếu, Inspector hiện cảnh báo và dạng đó sẽ không được đề xuất.
4. Kiểm tra vũ khí đã có trong **Weapons** của `LevelUpDraftConfig` (mục 14).

| Trường | Ý nghĩa | Lưu ý |
|---|---|---|
| Evolution ID | ID duy nhất trong vũ khí | **Bắt buộc** |
| Display Name | Tên trên thẻ | Thẻ hiện `Tên vũ khí Lv. N: Tên dạng` |
| Description | Mô tả trên thẻ | Nên điền để người chơi so sánh |
| Icon | Ảnh trên thẻ | Để trống thì dùng Icon của vũ khí |
| Attack | Đòn đánh của dạng này | Có thể là loại khác, ví dụ Aura đổi thành Projectile |
| Stat Bonuses, Stat Restrictions | Như level thường | Tác dụng lên **mọi vũ khí** của nhân vật |
| Triggered Effects | Hiệu ứng riêng của dạng | |

Dạng tối thượng không có trường chỉ số riêng. Muốn mạnh hơn, nhanh hơn hay to hơn, hãy dùng **Stat Bonuses** như mọi level khác (mục 13).

Bonus Attack Speed Percent không làm Aura đánh nhanh hơn. Muốn Aura có nhịp khác, hãy dùng Attack có **Hit Cooldown** khác.

Bonus âm chỉ trừ đến 0: tổng Attack Speed của nhân vật không xuống dưới 0, nên vũ khí không thể chậm hơn **Attack Interval**. Bonus âm cũng làm giảm tốc độ của mọi vũ khí khác.

### 21.3 Cách thẻ xuất hiện

- Ở level thường cuối cùng, lần nâng cấp tiếp theo hiện thẻ dạng tối thượng.
- Thẻ dạng tối thượng được ưu tiên trước các thẻ khác, tối đa bằng **Choice Count**.
- Có nhiều dạng hơn số thẻ thì chọn ngẫu nhiên.
- Có ít dạng hơn số thẻ thì các ô còn lại là thẻ thường, nên người chơi có thể chọn sau.
- Có nhiều vũ khí cùng sẵn sàng thì mỗi lần chỉ hiện dạng của một vũ khí, chọn ngẫu nhiên.
- Vũ khí chỉ có 1 level thường thì lần nâng cấp đầu tiên sau khi nhận vũ khí sẽ là chọn dạng.

### 21.4 Ý tưởng cấu hình

| Vũ khí | Dạng | Cấu hình |
|---|---|---|
| Melee | Chém rộng | Attack để trống, Stat Bonus Weapon Area Percent `100` |
| Melee | Chém nặng | Attack để trống, Stat Bonus Attack Power Percent `300`, Attack Speed Percent `-50` |
| Melee | Chém phóng đạn | Attack để trống, thêm Triggered Effect `OnAttack` với Effect Prefab có `SlashProjectileEffectActor` (mục 11) |
| Aura | To hơn | Attack để trống, Stat Bonus Weapon Area Percent `100` |
| Aura | Nhịp nhanh | Attack riêng có **Hit Cooldown** nhỏ hơn |
| Aura | Ném ra | Attack loại Projectile: Parabolic, `ExplodeAtEnd` |
| Projectile | Bắn nhanh | Attack để trống, Stat Bonus Attack Speed Percent `150` |
| Projectile | Một phát to, chậm | Attack với Shot Mode `Shot` và **Per Projectile Damage Multiplier** `5`, Stat Bonus Attack Speed Percent `-60` |
| Projectile | Xuyên toàn bộ | Attack có Impact Mode `PierceAll` |
| Beam | Chiếu liên tục | Attack có Beam Mode `Continuous`, Aim Tracking `FollowCursor` |

Dạng **Ném ra** của Aura có thể để lại vùng sát thương tồn tại lâu tại điểm rơi bằng **Ground Zone Prefab** (mục 22.4).

### 21.5 Chạy thử nhanh

1. Vào Play Mode, chọn object có `Weapon Controller`.
2. Bấm **Debug Level Up** đến level thường cuối cùng.
3. Ở mục **Debug Evolution**, bấm **Evolve: Tên dạng**. Mục **Runtime Level Values** hiện tên dạng đang dùng.

Muốn xem thẻ, chọn `Level Up Draft Controller` trên Iris rồi bấm **Open Draft For Current Level**.

### 21.6 Stack chí mạng khi tiến hoá

Mục **Evolution Passive** trong WeaponData áp dụng cho **mọi dạng tiến hoá** của vũ khí đó.

| Trường | Ý nghĩa |
|---|---|
| Enabled | Bật cơ chế |
| Crit Per Kill | Điểm chí mạng mỗi stack, mỗi kẻ địch bị vũ khí này giết cho 1 stack |
| Stack Duration | Số giây giữ stack. Mỗi lần giết đặt lại thời gian, hết giờ mất toàn bộ stack |
| Crit Cap | Chí mạng vượt mức này được đổi thành ATK |
| Excess Crit Per Atk Percent | Số điểm chí mạng đổi được 1% ATK bonus (theo ATK gốc). `2` là 2 điểm = 1% |
| Convert Other Crit Bonuses To Atk | Khi đã tiến hoá, mọi bonus chí mạng từ nguồn khác đổi vĩnh viễn thành ATK theo tỉ lệ trên. CRT gốc và stack khi giết vẫn là chí mạng |

## 22. Fire Zone và 2 dạng tiến hoá

Fire Zone là Aura có 3 level thường và 2 dạng tối thượng. Mọi file đã được tạo sẵn:

| File | Vai trò |
|---|---|
| `Aura/Weapons/FireZone.asset` | WeaponData và 2 dạng trong **Forms** |
| `Aura/Prefabs/FireAuraActor.prefab` | Vùng lửa đi theo nhân vật |
| `Aura/Prefabs/FireGroundZoneActor.prefab` | Vùng cháy tại điểm rơi |
| `Aura/Prefabs/FireThrowProjectile.prefab` | Viên lửa bay |
| `Aura/Prefabs/FireZoneWeapon.prefab` | **Acquisition Prefab** trong bảng nâng cấp |
| `Ranged/Projectile/Prefabs/ProjectileAttackActor.prefab` | Prefab điều phối đạn, dùng chung |
| `Aura/Attacks/FireAuraAttack.asset` | Attack của 3 level thường |
| `Aura/Attacks/FireBurnAuraAttack.asset` | Attack của dạng 1 |
| `Aura/Attacks/FireThrowAttack.asset` | Attack của dạng 2 |
| `Aura/Attacks/FireThrowProjectileDefinition.asset` | Đường bay và điểm nổ của dạng 2 |
| `Effects/Statuses/BurnStatus.asset` | Hiệu ứng thiêu đốt |
| `Effects/Applications/ApplyBurnEffect.asset` | Effect gây thiêu đốt |

### 22.1 Hiệu ứng thiêu đốt

Thiêu đốt gồm 2 asset, cả 2 dạng dùng chung.

**`BurnStatus`** (`Create > Weapon > Status > Damage Over Time`):

| Trường | Giá trị | Ý nghĩa |
|---|---|---|
| Duration | `4` | Thời gian cháy, được làm mới mỗi lần trúng |
| Maximum Stacks | `5` | Số stack tối đa |
| Stack Policy | `AddStackAndRefresh` | Mỗi lần trúng thêm 1 stack |
| Flat Damage | `2` | Damage mỗi stack mỗi nhịp |
| Source Damage Multiplier | `0.1` | Cộng thêm 10% damage của đòn gây cháy |
| Tick Interval | `1` | Số giây giữa hai nhịp cháy |
| Element | `Fire` | |
| Visual Style, Visual Color | `Burn`, màu cam | Particle trên kẻ địch |

**`ApplyBurnEffect`** (`Create > Weapon > Effect > Apply Status`): kéo `BurnStatus` vào **Status**.

Trong **Triggered Effects** của dạng tối thượng, thêm một dòng: **Trigger** `OnHit`, **Trigger Chance** `1`, **Effect** `ApplyBurnEffect`.

### 22.2 Dạng 1: Burning Field

Aura luôn đi theo nhân vật, tầm to hơn, gây thiêu đốt, và kẻ địch đang cháy nhận thêm damage.

| Muốn chỉnh | Sửa ở đâu |
|---|---|
| **Damage thêm lên kẻ địch đang cháy** | `FireBurnAuraAttack` > **Bonus Damage Percent** (đang là `20`) |
| Tầm to hơn | Dạng `burning_field` > **Stat Bonuses** > Weapon Area Percent (đang là `50`) |
| Bán kính gốc | `FireAuraActor` > `Circle Collider 2D` > **Radius** (đang là `4`), nhớ chỉnh Scale của `Visual` theo |
| Nhịp gây sát thương | `FireBurnAuraAttack` > **Hit Cooldown** (đang là `0.5`) |
| Sức mạnh thiêu đốt | `BurnStatus` |

Bonus Weapon Area là chỉ số nhân vật, nên cũng làm to các vũ khí khác. Kẻ địch bắt đầu cháy từ nhịp sát thương đầu tiên sau khi vào vùng, nên nhịp đó chưa có +20%.

### 22.3 Dạng 2: Fire Throw

Mỗi đòn ném liên tục N quả cầu lửa về phía chuột, quả này nối quả kia. Mỗi quả bay theo đường cong ngẫu nhiên, rơi ở một điểm ngẫu nhiên quanh chuột, nổ và để lại vùng cháy.

| Muốn chỉnh | Sửa ở đâu |
|---|---|
| **Số viên lửa** | `FireThrowAttack` > **Projectile Count** (đang là `5`) |
| **Kiểu ném** | `FireThrowAttack` > **Shot Mode**: `Burst` ném lần lượt (đang dùng), `Multishot` ném cùng lúc |
| **Khoảng cách giữa 2 quả** | `FireThrowAttack` > **Burst Shot Interval** (đang là `0.15`), Haste rút ngắn |
| Độ cong đường bay | `FireThrowProjectileDefinition` > bật **Randomize Arc Height**, đặt **Minimum/Maximum Arc Height** (đang là `0.5`–`2`), **Arc Side** `Random` |
| **Độ tản của điểm rơi** | `FireThrowAttack` > **Landing Spread Radius** (đang là `3`) |
| **Damage mỗi nhịp của vùng cháy** | `FireGroundZoneActor` > `Weapon Aura Actor` > **Damage Multiplier** (đang là `1.5`) |
| Thời gian tồn tại của vùng | `FireGroundZoneActor` > `Weapon Ground Zone Actor` > **Duration** (đang là `3`) |
| Nhịp của vùng | `FireGroundZoneActor` > `Weapon Ground Zone Actor` > **Tick Interval** (đang là `0.5`) |
| Bán kính vùng | `FireGroundZoneActor` > `Circle Collider 2D` > **Radius** (đang là `2.5`), nhớ chỉnh Scale của `Visual` theo |
| Damage khi nổ | `FireThrowProjectileDefinition` > **Explosion Damage Multiplier** (đang là `1`) |
| Tầm ném | `FireThrowProjectileDefinition` > **Maximum Range** (đang là `12`) |
| Tốc độ ném | `FireZone` > **Attack Interval** (đang là `3`), có chịu Haste |

Điểm rơi nằm trong vòng **Landing Spread Radius** quanh chuột. Chuột ở quá xa thì tâm vòng được kéo vào trong tầm, để cả vòng vẫn nằm trong **Maximum Range**.

### 22.4 Tự tạo vùng cháy tại điểm rơi

1. `Create Empty`, đặt tên ví dụ `MyGroundZone`.
2. **Add Component** `Weapon Ground Zone Actor`. Unity tự thêm `Weapon Aura Actor` và `Circle Collider 2D`.
3. Trong `Circle Collider 2D`: bật **Is Trigger**, đặt **Radius**.
4. Tạo object con cho hình ảnh và kéo vào **Visual Root** của `Weapon Aura Actor`. Kéo collider vào **Detection Area**.
5. Đặt **Damage Multiplier** của `Weapon Aura Actor`, **Duration** và **Tick Interval** của `Weapon Ground Zone Actor`.
6. Lưu thành prefab, kéo vào **Ground Zone Prefab** của ProjectileDefinition. Đặt **Impact Mode** là `ExplodeAtEnd`.

Hit của vùng cháy kích hoạt **Triggered Effects** `OnHit` của dạng đang dùng, nên chỉ cần thêm `ApplyBurnEffect` là vùng gây thiêu đốt.

### 22.5 Công thức

```text
Damage vùng lửa  = Base Damage × (ATK / 100) × Damage Multiplier × (1 + Bonus Damage Percent / 100 nếu mục tiêu đang cháy)
Damage khi nổ    = Base Damage × (ATK / 100) × Per Projectile Damage Multiplier × Explosion Damage Multiplier
Damage thiêu đốt = (Flat Damage + damage đòn gây cháy × Source Damage Multiplier) × số stack, mỗi Tick Interval
```

### 22.6 Lưu ý

- **Không xoá `FireZoneWeapon`:** Fire Zone cần **Acquisition Prefab** vì level 1 là Aura (mục 18).
- **Dạng 2 thay Aura:** sau khi chọn Fire Throw, vùng lửa quanh nhân vật biến mất.
- **Vùng cháy không bị vật cản chặn:** đạn đang để **Obstacle Layer Mask** là `Nothing`, nên bay và rơi qua vật cản.

## 23. Skill Ultimate của nhân vật

Ultimate là skill chủ động, kích hoạt bằng phím `X`. Skill có thể chỉ cấp chỉ số trong một khoảng thời gian, hoặc tấn công bằng một WeaponData cấu hình như vũ khí thường.

```text
Input (_ultimate) ──> CharacterUltimateController ──> UltimateData
                                                       ├─ Stat Bonuses, Invulnerable
                                                       └─ Attack Weapon: WeaponData
```

### 23.1 UltimateData

**Tạo:** `Create > Character > Ultimate Data`. Asset hiện có là `GameContent/Player/Iris/Data/IrisUltimate.asset`.

| Trường | Ý nghĩa | Lưu ý |
|---|---|---|
| Ultimate ID, Display Name, Description, Icon | Thông tin nhận diện | |
| Cooldown | Số giây chờ trước lần dùng tiếp theo | Tính từ lúc skill **kết thúc** |
| Initial Cooldown | Số giây chờ trước lần dùng đầu tiên | `0` là dùng được ngay |
| Duration | Số giây skill hoạt động | `0` là tức thời: Attack Weapon đánh đúng một lần |
| Attack Weapon | WeaponData của đòn đánh | Để trống thì skill chỉ cấp chỉ số |
| Stat Bonuses | Bonus trong lúc skill hoạt động | Tính như mục 13. Không có tác dụng khi Duration bằng `0` |
| Invulnerable While Active | Không nhận sát thương trong lúc skill hoạt động | Không có tác dụng khi Duration bằng `0` |

`IrisUltimate` hiện đặt Duration `5`, Cooldown `30`, Attack Speed Percent `150`, Movement Speed Percent `100`, bất tử và chưa có Attack Weapon.

### 23.2 Gắn skill đánh vào Ultimate

1. Tạo một WeaponData theo mục 3. Skill **chỉ dùng level đầu tiên**, nên chỉ cần 1 level.
2. Không thêm WeaponData này vào `LevelUpDraftConfig`. Vũ khí của skill không chiếm slot và không xuất hiện trên thẻ nâng cấp.
3. Kéo WeaponData vào **Attack Weapon** của UltimateData.

| Duration | Attack Transient | Attack Persistent (Aura) |
|---|---|---|
| Lớn hơn `0` | Tự đánh theo **Attack Interval** trong suốt thời gian skill | Aura chỉ tồn tại trong lúc skill hoạt động |
| Bằng `0` | Đánh đúng một lần | **Không đánh**, Console có cảnh báo |

Stat Bonuses và Triggered Effects ở level đầu tiên của WeaponData vẫn hoạt động, nhưng bonus chỉ có tác dụng trong lúc skill chạy.

### 23.3 Gắn Ultimate cho nhân vật mới

1. Thêm component `Character Ultimate Controller` vào cùng object có `Player Health` trên prefab nhân vật, rồi kéo UltimateData vào **Ultimate**.
2. Trong component `Input` của nhân vật, kéo controller vừa thêm vào trường **Ultimate**.
3. **Combat Stats**, **Player Health**, **Attack Origin** và **Aim Provider Behaviour** có thể để trống, code sẽ tự tìm trên prefab.

Khi đang mở bảng nâng cấp, game bị tạm dừng nên không kích hoạt được Ultimate.
