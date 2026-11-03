# Hướng dẫn cấu hình vũ khí

Tài liệu này mô tả hệ thống vũ khí đúng theo code hiện tại, kèm từng thao tác trong Unity. Nếu chỉ cần làm nhanh, đi thẳng tới mục 3 rồi tới phần của loại vũ khí bạn muốn tạo.

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

Mỗi level có Attack riêng, nên một vũ khí có thể đổi hẳn loại đòn đánh giữa các level. Ví dụ Iris Slash chém ở level 1 đến 3 và thành aura lửa ở level 4.

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
│  ├─ Player/ Enemies/ Environment/
│  └─ Weapons/
│     ├─ Effects/               Applications, Direct, Statuses
│     ├─ Melee/  Aura/          Attacks, Prefabs, Visuals, Weapons
│     └─ Ranged/Projectile/     Definitions, Attacks, Prefabs, Visuals, Weapons
├─ Resources/                   Config nạp bằng tên: LevelUpDraftConfig, ExperienceSystemConfig
└─ Scenes/                      TestLevel.unity
```

Quy tắc đặt file:

- Code luôn nằm trong `Scripts`, asset và prefab luôn nằm trong `GameContent`.
- Luôn di chuyển asset **bên trong cửa sổ Project của Unity** để file `.meta` đi cùng và tham chiếu không bị mất.
- Beam chưa có content, nên khi tạo hãy dùng cấu trúc `GameContent/Weapons/Ranged/Beam/{Attacks,Prefabs,Weapons}`.

## 3. Quy trình tạo một vũ khí

Áp dụng cho mọi loại vũ khí:

1. **Tạo Attack Prefab** theo loại vũ khí (mục 6, 7, 8 hoặc 9). Projectile có sẵn prefab dùng lại được.
2. **Tạo asset Attack:** trong cửa sổ Project, chuột phải vào folder `Attacks` rồi chọn `Create > Weapon > ...`. Kéo prefab vào trường **Attack Prefab**.
3. **Tạo WeaponData:** chuột phải vào folder `Weapons` rồi chọn `Create > Weapon > Weapon Data`. Điền ID, Base Damage và **Target Layer Mask = Enemy**.
4. **Thêm level:** ở danh sách **Levels**, bấm `+`. Điền Attack Interval, Damage, Area Multiplier và kéo asset Attack vào trường **Attack**.
5. **Tách dữ liệu theo level:** trong mục **Full Level Attack Profiles** ở cuối Inspector, bấm **Make Every Level Independent** (xem mục 4.3).
6. **Gắn vào nhân vật** (mục 10) hoặc **đưa vào bảng nâng cấp** (mục 14).
7. **Chạy thử** (mục 16).

Ba trường dễ quên nhất khiến vũ khí không gây sát thương: **Target Layer Mask**, **Damage** của level và **Attack**.

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
| Levels | Danh sách level | Cần ít nhất 1 phần tử |

### 4.2 Trường của mỗi level

| Trường | Ý nghĩa | Lưu ý |
|---|---|---|
| Description | Mô tả trên thẻ nâng cấp | Nên điền, nếu trống thẻ hiện chữ mặc định |
| Attack Interval | Số giây giữa hai lần đánh | Chỉ dùng cho Transient. Aura dùng Hit Cooldown |
| Minimum Attack Interval | Sàn của interval sau khi tính Haste | `-1` là không giới hạn |
| Damage | Hệ số damage của level | `10` nghĩa là gấp 10 lần Base Damage |
| Area Multiplier | Hệ số kích thước của đòn đánh | `1` là mặc định. **Đừng đặt `0`**: đòn đánh sẽ có kích thước 0 và không trúng ai |
| Stat Bonuses | Bonus cho nhân vật khi đạt level này | Cộng dồn với các level trước (mục 13) |
| Stat Restrictions | Luật chặn hoặc đổi chỉ số | Mục 13 |
| Attack | Asset đòn đánh của level | Bắt buộc |
| Triggered Effects | Hiệu ứng của level | Không tự kế thừa. Muốn level sau vẫn có thì phải thêm lại |

Số level tối đa của vũ khí chính là số phần tử trong **Levels**.

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
| Hit Limit | Số mục tiêu tối đa mỗi lần đánh, `-1` là không giới hạn | ✓ | ✓ | | |
| Hit Cooldown | Chu kỳ gây sát thương | | ✓ | | |
| Hit Rule | **Chưa có tác dụng** (mục 18) | | | | |

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

### 7.2 Tạo asset Attack

`Create > Weapon > Attack Definition`, lưu tại `Aura/Attacks/`:

- **Lifetime = Persistent**. Đây là điểm bắt buộc của Aura.
- **Attack Prefab** = prefab vừa tạo.
- **Hit Cooldown** = số giây giữa hai lần gây sát thương, ví dụ `0.5`.
- **Hit Limit** = số mục tiêu tối đa mỗi nhịp, `-1` là không giới hạn.

Với Aura, **Attack Interval** của level không được dùng và **Haste không rút ngắn Hit Cooldown**.

## 8. Vũ khí bắn đạn (Projectile)

Projectile có 3 lớp:

```text
ProjectileWeaponAttackDefinition   Bắn thế nào: mấy viên, kiểu loạt, hệ số damage
  ├─ Attack Prefab: ProjectileAttackActor.prefab   Dùng lại, không cần tạo mới
  └─ ProjectileDefinition          Đạn bay và va chạm thế nào
       └─ Projectile Prefab        Hình ảnh viên đạn
```

### 8.1 Tạo prefab viên đạn

1. `Create Empty`, đặt tên ví dụ `MyBulletProjectile`.
2. **Add Component** `Weapon Projectile Actor`. Component này không có trường nào cần điền.
3. Thêm `Sprite Renderer` trên chính object hoặc object con, có thể thêm `Trail Renderer`.
4. **Hướng vẽ:** đầu viên đạn phải hướng theo trục `+X`, tức là sang phải.
5. **Không cần** `Rigidbody2D` hay `Collider2D`, vì đạn tự quét va chạm bằng Raycast hoặc CircleCast.
6. Lưu prefab vào `Ranged/Projectile/Prefabs/`.

### 8.2 ProjectileDefinition

**Tạo:** `Create > Weapon > Projectile > Definition`, lưu tại `Ranged/Projectile/Definitions/`.

**Cơ bản**

| Trường | Ý nghĩa |
|---|---|
| Projectile Prefab | Prefab viên đạn ở mục 8.1 |
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

**VFX**

| Trường | Ý nghĩa |
|---|---|
| Impact Vfx Prefab | Hiệu ứng khi đạn trúng trực tiếp hoặc chạm vật cản |
| Explosion Vfx Prefab | Hiệu ứng khi nổ |
| Vfx Lifetime | Thời gian tự huỷ của hiệu ứng |

### 8.3 ProjectileWeaponAttackDefinition

**Tạo:** `Create > Weapon > Attack > Projectile`, lưu tại `Ranged/Projectile/Attacks/`.

Đặt **Lifetime = Transient** và **Attack Prefab = `Ranged/Projectile/Prefabs/ProjectileAttackActor.prefab`**. Không kéo prefab viên đạn vào đây.

| Trường | Ý nghĩa |
|---|---|
| Projectile | ProjectileDefinition ở mục 8.2 |
| Shot Mode | `Shot` 1 viên, `Multishot` nhiều viên cùng lúc, `Burst` nhiều viên liên tiếp, `BurstSequence` lặp lại nhiều loạt Burst |
| Spawn Pattern | `Forward` cùng hướng, `Cone` toả đều trong góc |
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

1. Mở `GameContent/Player/Prefabs/Iris.prefab` bằng cách double-click.
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

## 11. Triggered Effects: OnAttack, OnHit, OnKill

Thêm trong `WeaponData > Levels > Element N > Triggered Effects`, bấm `+`:

| Trường | Ý nghĩa |
|---|---|
| Trigger | `OnAttack` mỗi lần đánh. `OnHit` mỗi hit được chấp nhận. `OnKill` khi hit đó giết mục tiêu |
| Trigger Chance | `1` là 100%, `0.25` là 25% |
| Internal Cooldown | Cooldown riêng của dòng này trên vũ khí, giây |
| Allow Secondary Hits | Cho phép kích hoạt từ hit phụ như status, mark hay hit do effect tạo ra. Nên để tắt |
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

Các asset mẫu nằm ở `GameContent/Weapons/Effects/`. Chúng dùng được ngay, hiện chưa gắn vào vũ khí nào.

| Thư mục | Asset |
|---|---|
| `Applications/` | ApplyBurnEffect, ApplyMarkEffect, ApplyPoisonEffect, ApplySlowEffect, ApplyStunEffect |
| `Direct/` | KnockbackEffect, MarkBurstDamage |
| `Statuses/` | BurnStatus, MarkStatus, PoisonStatus, SlowStatus, StunStatus |

**Dùng asset có sẵn:** kéo `ApplyBurnEffect` vào trường **Effect** của Triggered Effect, đặt **Trigger = OnHit**.

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

Ví dụ `MarkStatus`: đạt 3 stack thì chạy `MarkBurstDamage` và tiêu 3 stack.

### 12.3 Yêu cầu trên kẻ địch

| Tính năng | Enemy cần có | Prefab Enemy hiện tại |
|---|---|---|
| Nhận sát thương | `Collider2D` trên layer `Enemy` và component `IDamageReceiver` | ✓ (`EnemyHealth`) |
| Nhận status | `StatusEffectController` cùng object với `EnemyHealth` | ✓ |
| Thấy particle status | `StatusEffectVisualController` | ✓ |
| Slow, Stun, Knockback | `EnemyFlowFieldAgent` | ✓ |

## 13. Bonus chỉ số và luật chỉ số

### Stat Bonuses

Mỗi level cấp thêm bonus cho **nhân vật**, không chỉ cho vũ khí đó. Bonus cộng dồn từ level 1 đến level hiện tại. Giá trị tính theo điểm phần trăm: `30` nghĩa là +30.

| Stat | Tác dụng |
|---|---|
| Attack Power Percent | Tăng damage |
| Critical Chance Percent | Tăng tỉ lệ chí mạng |
| Attack Speed Percent | Haste: giảm Attack Interval, tăng tốc animation và burst |
| Movement Speed Percent | Tăng tốc độ di chuyển |
| Weapon Area Percent | Tăng kích thước **mọi** vũ khí |
| Experience Pickup Range Percent | Tăng tầm nhặt EXP |

### Stat Restrictions

Luật này chặn hoặc chuyển đổi chỉ số đến từ nguồn khác:

| Trường | Ý nghĩa |
|---|---|
| Stat | Chỉ số bị ảnh hưởng |
| Affected Sources | Nguồn bị ảnh hưởng: BaseStats, InspectorBonus, WeaponLevel, Equipment, Passive, TemporaryBuff |
| Mode | `Block` bỏ giá trị. `Convert` đổi sang chỉ số khác |
| Converted Stat, Conversion Ratio | Chỉ số đích và tỉ lệ đổi, chỉ dùng với `Convert` |
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
Damage = Base Damage × (ATK / 100) × Level.Damage × Hệ số phụ
Damage = Damage × ngẫu nhiên(1 - Variance/100, 1 + Variance/100)
Nếu chí mạng: Damage = Damage × Critical Hit Multiplier
Làm tròn thành số nguyên
```

Hệ số phụ là `Per Projectile Damage Multiplier`, `Explosion Damage Multiplier`, `Damage Multiplier Per Tick`, `Damage Multiplier` của Triggered Effect, hoặc hệ số bounce, pierce tuỳ trường hợp.

Ví dụ đo được khi chạy thử, với ATK = 150:

- Iris Slash level 1: `10 × 1.5 × 10 = 150`, chí mạng `150 × 1.75 = 262`.
- Bomb nổ: `10 × 1.5 × 1 × 0.4 = 6`, chí mạng `6 × 1.75 = 10`.

**Tốc độ đánh** (khi bật Affected By Haste)

```text
Interval = Attack Interval / (1 + Haste / 100)
Interval = max(Interval, Minimum Attack Interval)
Tốc độ animation = 1 + Haste / 100
```

**Kích thước**

```text
Hệ số Area = Level.Area Multiplier × (Weapon Area / 100)
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
| Hiệu ứng level 2 mất ở level 3 | Triggered Effects không kế thừa | Thêm lại ở level 3 |
| `OnAttack` với Apply Status không có tác dụng | OnAttack chưa có mục tiêu | Dùng OnHit |
| Hiệu ứng kích hoạt quá nhiều | Nhiều viên đạn hoặc tia nhiều nhịp | Tăng Internal Cooldown, dùng OncePerVolley hoặc OncePerActivation |
| Vũ khí mới không xuất hiện khi lên cấp | Hết slot, chưa đủ level, hoặc chưa có trong config | Xem mục 14 |

## 18. Giới hạn hiện tại của code

Những điểm dưới đây là hành vi thực tế của code, nên tính đến khi cấu hình:

- **Hit Rule không có tác dụng.** Melee luôn đánh mỗi mục tiêu một lần trong một đòn. Hit Cooldown chỉ dùng cho Aura.
- **Element chưa ảnh hưởng gameplay.** Giá trị được ghi vào damage nhưng chưa có kháng nguyên tố, và damage chính của vũ khí luôn là `Physical`.
- **Aura ở level 1 khi nhận qua bảng nâng cấp mà không có Acquisition Prefab sẽ có bán kính gần 0**, do actor được khởi tạo trước `Awake`. Hãy dùng Acquisition Prefab. Đổi sang Aura ở level sau, như Iris Slash level 4, thì không bị.
- **`TargetBounce`:** `Prevent Repeat Targets = false` không cho phép đánh lại mục tiêu cũ, và một lần va chạm lặp với mục tiêu cũ có thể tiêu mất một lượt nảy.
- **`PlayerHealth`:** HP trong `CharacterStats` chưa được dùng, máu lấy từ `Max Health`. Khi hết máu chưa có xử lý thua cuộc.

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
| **Vùng lửa** | Aura, Persistent, Hit Cooldown 0.5. Thêm OnHit ApplyBurnEffect, Internal Cooldown 0.1 |
| **Chém làm chậm** | Melee. Thêm OnHit ApplySlowEffect, Chance 1 |

Khi dùng Pierced `1`, đạn biến mất sau mục tiêu đầu tiên.

## 20. Vũ khí đang có trong project

**Iris Slash** (`Melee/Weapons/IrisSlashWeapon.asset`): Base Damage 10, Critical 1.75, Variance 0.

| Level | Interval | Damage | Area | Bonus | Attack |
|---|---:|---:|---:|---|---|
| 1 | 1 (min 0.5) | 10 | 1 | ATK +10, Haste +12, Move +10 | Slash |
| 2 | 1 | 12 | 1 | Haste +10 | Slash |
| 3 | 1 | 15 | **0** | Haste +40, Area +100 | Slash |
| 4 | 1 | 12 | 1 | ATK +20, Move +100 | Aura lửa |

Level 3 có Area Multiplier bằng 0, nên đòn chém ở level này không trúng mục tiêu. Hãy sửa giá trị này nếu đó không phải chủ đích.

**Triple Arc Bomb** (`Ranged/Projectile/Weapons/TripleArcBombWeapon.asset`): Base Damage 10, Interval 1.5, Parabolic, ExplodeAtEnd, Radius 2.5, hệ số 0.4.

| Level | Kiểu bắn | Bonus |
|---|---|---|
| 1 | Burst 2 viên | Area +50 |
| 2 | BurstSequence 3 viên × 2 loạt | Pickup Range +120 |

**Fire Zone** (`Aura/Weapons/FireZone.asset`): Aura, Base Damage 1, Hit Cooldown 0.5, bán kính 5.7. Chưa gắn cho nhân vật và chưa có trong bảng nâng cấp, nhưng Attack của nó đang được Iris Slash level 4 dùng.

Iris đang gắn sẵn Iris Slash và Triple Arc Bomb, nên đã kín 2 slot vũ khí.
