# Hướng dẫn cấu hình toàn bộ Weapon System

Tài liệu này là nguồn hướng dẫn chính cho toàn bộ vũ khí hiện tại: melee, aura, projectile Shot/Multishot/Burst/Burst Sequence, Beam, OnHit, Status và hiệu ứng theo level.

## 1. Cấu trúc sau khi sắp xếp

`Assets/WeaponSystem` chỉ chứa framework:

- `Data`: kiểu dữ liệu chung và ScriptableObject definition.
- `Runtime`: controller, attack actor, damage delivery và projectile runtime.
- `Effects/Data`: class định nghĩa effect/status.
- `Effects/Runtime`: stack, timer, receiver và visual runtime.
- `Editor`: custom Inspector và công cụ authoring.
- `Documentation`: các hướng dẫn.

Content cấu hình nằm tại `Assets/GameContent/Weapons`:

```text
Weapons
├─ Effects
│  ├─ Applications
│  ├─ Direct
│  └─ Statuses
├─ Melee
│  ├─ Attacks
│  ├─ Prefabs
│  ├─ Visuals
│  └─ Weapons
├─ Aura
│  ├─ Attacks
│  ├─ Prefabs
│  ├─ Visuals
│  └─ Weapons
└─ Ranged
   ├─ Projectile
   │  ├─ Definitions
   │  ├─ Attacks
   │  ├─ Prefabs
   │  └─ Weapons
   └─ Beam
      ├─ Attacks
      ├─ Prefabs
      └─ Weapons
```

Các thư mục ranged chưa chứa content gameplay dựng sẵn. Chúng là vị trí author asset mới. Hai prefab actor framework trong `Projectile/Prefabs` và `Beam/Prefabs` được giữ để AttackDefinition sử dụng.

## 2. Chuỗi asset của một vũ khí

Mọi vũ khí đi qua chuỗi sau:

```text
WeaponController
  → WeaponData
    → WeaponLevelData
      → WeaponAttackDefinition
        → Attack Actor Prefab
      → Triggered Effects
        → WeaponEffectDefinition hoặc Effect Actor Prefab
```

Projectile có thêm:

```text
ProjectileWeaponAttackDefinition
  → ProjectileDefinition
    → Projectile Visual Prefab
```

Các lớp có trách nhiệm tách biệt:

- ScriptableObject chứa cấu hình dùng chung, không chứa state của một lần bắn.
- MonoBehaviour actor chứa state runtime của attack/projectile đang tồn tại.
- `WeaponController` quản lý level, cooldown, spawn attack và dispatch OnAttack/OnHit/OnKill.

## 3. Công thức damage

Damage primary trước variance và crit:

```text
Base Damage
× Effective ATK / 100
× Level Damage
× Delivery Multiplier
```

`Delivery Multiplier` là `Per Projectile Damage Multiplier`, `Damage Multiplier Per Tick`, multiplier bounce/pierce/explosion hoặc multiplier của triggered actor.

Sau đó hệ thống:

1. Random variance trong khoảng `1 ± Damage Variance Percent / 100`.
2. Roll crit bằng `Effective CRT`.
3. Nếu crit, nhân `Critical Hit Multiplier`.
4. Làm tròn về số nguyên.

## 4. WeaponController trên prefab chủ vũ khí

Mỗi weapon instance cần một `WeaponController`.

| Trường | Ý nghĩa |
|---|---|
| Weapon Data | Asset `WeaponData` được instance này sử dụng. |
| Starting Level | Index level bắt đầu. Level đầu tiên là 0. Giá trị vượt phạm vi được clamp. |
| Combat Stats | `CharacterCombatSystem` cung cấp ATK, CRT, Attack Speed, Movement Speed và Weapon Area. Nếu để trống, controller chỉ tự tìm trên cùng GameObject. Nếu component nằm ở parent/sibling thì phải kéo tay. |
| Attack Origin | Điểm spawn attack/projectile và đầu Beam. Để trống dùng Transform của chính WeaponController. |
| Aim Provider Behaviour | MonoBehaviour triển khai `IWeaponAimProvider2D`; dự án dùng `LookAtMouse`. Để trống sẽ tìm trong parent. |
| Auto Attack | Bật: tự gọi attack khi cooldown bằng 0. Tắt: chỉ bắn khi code gọi `TryAttack()`. |
| Attack Immediately | Bật: lần đầu bắn ngay khi enable. Tắt: chờ đủ Attack Interval đầu tiên. |
| Current Level Index | Runtime debug; không dùng để author level bắt đầu. |
| Remaining Cooldown | Runtime debug số giây còn lại. |

Lưu ý aim:

- Shot, Multishot, Burst, Burst Sequence và projectile cong chụp hướng/điểm chuột khi kích hoạt.
- Projectile không đổi endpoint khi chuột di chuyển sau đó.
- Beam `FollowCursor` đọc lại chuột trong lúc Beam đang tồn tại.

## 5. CharacterCombatSystem

| Trường | Ý nghĩa |
|---|---|
| Base Stats | `CharacterStats` gốc. Nếu null: ATK/SPD mặc định 100, CRT/Attack Speed mặc định 0, Weapon Area mặc định 100. |
| Inspector Attack Bonus | Cộng trực tiếp vào ATK phần trăm. ATK 120 + bonus 20 = 140%. |
| Inspector Critical Hit Bonus | Cộng vào xác suất crit phần trăm. |
| Inspector Attack Speed Bonus | Cộng vào Attack Speed/Haste phần trăm. |
| Inspector Movement Speed Bonus | Cộng vào SPD phần trăm; `PlayerMovement` nhân tốc độ gốc với kết quả / 100. |
| Inspector Weapon Area Bonus | Cộng vào area/range phần trăm; kích thước object attack/effect, melee hitbox, aura, projectile và Beam nhận stat này. |

Mọi bonus runtime có `CombatStatSource`: `BaseStats`, `InspectorBonus`, `WeaponLevel`, `Equipment`, `Passive`, `TemporaryBuff`. `CharacterCombatSystem` tính stat cuối toàn player và áp dụng các restriction trước khi consumer đọc giá trị.

API overload có `providerKey` và `ownerKey` cho phép nhiều provider thuộc cùng một trang bị. Đây là cách bonus phát sinh sau này như kill-stack của weapon vẫn được xem là “nguồn của chính weapon”. `RemoveStatProvider(providerKey)` gỡ cả bonus và restriction khi unequip/disable.

Nếu `Affected By Haste` bật:

```text
Final Interval = Attack Interval / (1 + Haste / 100)
```

Sau đó interval không được thấp hơn `Minimum Attack Interval` nếu min khác `-1`.

## 6. WeaponData: tất cả trường

Tạo bằng `Create > Weapon > Weapon Data`.

### Identity

| Trường | Ý nghĩa |
|---|---|
| Weapon ID | ID ổn định và duy nhất dùng cho logic/save trong tương lai. Không nên đổi theo tên hiển thị. |
| Display Name | Tên UI. |
| Icon | Sprite UI; không ảnh hưởng gameplay. |

### Base Damage

| Trường | Ý nghĩa |
|---|---|
| Base Damage | Damage gốc trước ATK, level và delivery multiplier. |
| Critical Hit Multiplier | Hệ số khi crit. `1.75` nghĩa là 175%. Không nhỏ hơn 1. |
| Damage Variance Percent | Dao động damage hai phía. `10` nghĩa là từ 90% đến 110%. Đặt 0 khi cân bằng/test chính xác. |

### Rules

| Trường | Ý nghĩa |
|---|---|
| Affected By Haste | Bật để Haste giảm Attack Interval. Tắt cho aura/turret cần tick cố định. |
| Target Layer Mask | Layer mục tiêu hợp lệ. Dự án hiện dùng Enemy layer bit 3 (`m_Bits = 8`). Không dùng mask này cho obstacle. |

### Levels

Mỗi phần tử là một cấu hình hoàn chỉnh của một level.

| Trường | Ý nghĩa |
|---|---|
| Description | Mô tả thay đổi của level, chỉ để author/UI. |
| Attack Interval | Thời gian giữa hai activation của attack Transient. |
| Minimum Attack Interval | Sàn sau Haste. `-1` bỏ giới hạn sàn. |
| Damage | Hệ số damage của level. `3` nghĩa là nhân 3 Base Damage. |
| Area Multiplier | Hệ số kích thước của level. Runtime scale đồng nhất object attack/effect; melee hitbox, aura collider/visual, projectile collision/explosion/bounce/VFX và Beam width/query/VFX đều dùng cùng hệ số Area cuối. |
| Stat Bonuses | Bonus được cấp khi đạt đúng level này. Các level trước vẫn cộng dồn. `30` nghĩa là +30 percentage points; ví dụ CRT 25 + 30 = 55%, SPD 170 + 30 = 200%, Weapon Area 100 + 30 = 130%. |
| Player Stat Rules Unlocked At This Level | Luật Block/Convert stat ở cấp player, cộng dồn từ level mở khóa. `Allow Owning Source` cho phép chính weapon và provider cùng owner key vượt qua luật. |
| Attack | Profile `WeaponAttackDefinition` hoàn chỉnh của level. Profile có thể là sub-asset do chính level sở hữu và có thể đổi cả loại attack, actor prefab, animation hoặc mechanic giữa các level. |
| Triggered Effects | Danh sách OnAttack, OnHit, OnKill của level. |

Custom Inspector của `WeaponData` có mục `Full Level Attack Profiles`. Dùng `Make Every Level Independent` để clone toàn bộ profile hoặc `Make Complete Independent Copy For This Level` cho một level. Với Projectile, thao tác clone cả `ProjectileDefinition`; vì vậy trajectory, speed, impact, explosion, VFX, số viên và burst đều độc lập. Profile hoàn chỉnh được vẽ ngay bên dưới từng level để chỉnh trực tiếp.

### Hiệu ứng mở khóa theo level

Không còn hệ thống hay trạng thái Awakening riêng. Muốn một hiệu ứng xuất hiện ở level nào, cấu hình trực tiếp trong `Triggered Effects` của `WeaponLevelData` đó với trigger `OnAttack`, `OnHit` hoặc `OnKill`.

Mỗi level là một cấu hình hoàn chỉnh. Nếu hiệu ứng cần tiếp tục hoạt động ở level sau, hãy giữ/copy entry đó sang `Triggered Effects` của các level sau.

## 7. WeaponAttackDefinition: trường nền dùng chung

| Trường | Ý nghĩa và phạm vi hiện tại |
|---|---|
| Attack ID | ID của kiểu attack. |
| Display Name | Tên hiển thị của attack. |
| Lifetime = Transient | Mỗi activation spawn một actor mới. Dùng cho melee slash, projectile và Beam. |
| Lifetime = Persistent | Actor tồn tại cùng WeaponController và nhận `ExecuteAttack()` theo chu kỳ. Dùng cho Aura. |
| Attack Prefab | Prefab phải có component kế thừa `WeaponAttackActorBase` đúng với definition. |
| Animator State Name | Melee `WeaponAttackActor` gọi state này. Projectile, Beam và Aura hiện bỏ qua. |
| Hit Rule | Enum nền cũ. Runtime hiện tại chưa dùng trường này để thay đổi melee/projectile; không dựa vào nó để chống hit trùng. |
| Hit Limit | Số target tối đa mỗi activation, `-1` là không giới hạn. Melee/Aura dùng; projectile/Beam có policy riêng. |
| Hit Cooldown | Với attack Persistent, đây là chu kỳ `ExecuteAttack()`. Với Transient hiện không điều khiển cooldown weapon; dùng `Level.Attack Interval`. |

Các lựa chọn `Hit Rule` đang được giữ để mở rộng:

- `OnePerAttack`: dự kiến một target một lần trong attack.
- `OnePerWindow`: dự kiến một target một lần mỗi hitbox window.
- `RepeatWithCooldown`: dự kiến cho phép đánh lại sau cooldown.

Hiện `WeaponAttackActor` thực tế dùng HashSet và luôn chặn cùng target trong một instance slash.

## 8. Cấu hình melee đầy đủ

### 8.1 Melee Attack Data

Tạo `Create > Weapon > Attack Definition`.

Thiết lập chuẩn:

- `Lifetime = Transient`.
- `Attack Prefab` trỏ tới prefab có `WeaponAttackActor`.
- `Animator State Name` phải khớp tên state trong Animator Controller.
- `Hit Limit = -1` nếu slash được đánh mọi enemy trong hitbox.

### 8.2 Prefab melee attack

Prefab `SlashAttackActor` hiện có:

#### SpriteRenderer

- `Sprite`: visual của slash.
- `Color`: tint/alpha.
- `Sorting Layer` và `Order In Layer`: thứ tự vẽ.
- Flip X/Y chỉ đổi visual, không tự đổi dữ liệu hitbox.

#### Animator

- `Controller`: Animator Controller chứa state được khai báo ở Attack Data.
- `Speed`: runtime bị nhân theo Haste cho attack Transient.
- `Apply Root Motion`: nên tắt cho attack visual 2D.

#### AnimationHitboxTrack

| Trường | Ý nghĩa |
|---|---|
| Animator | Animator mà hitbox bám theo. |
| Animation Clip | Clip dùng để quy đổi normalized time thành frame. |
| Target Layer | Mask preview/fallback; khi runtime WeaponAttackActor khởi tạo, giá trị được thay bằng `WeaponData.Target Layer Mask`. |
| Detection Mode = Physics2D | Quét Collider2D. Phù hợp project hiện tại. |
| Detection Mode = Physics3D | Quét Collider 3D. |
| Detection Mode = Both | Chạy cả hai loại query. Chỉ bật nếu thật sự dùng cả 2D và 3D. |
| Detection Depth | Độ sâu box cho Physics3D; ít ý nghĩa với Physics2D. |
| On Target Detected | UnityEvent thô mỗi lần scan thấy target. Weapon system dùng event code `ContactDetected`, không cần gắn callback Inspector. |
| Activation Mode = FramesWithHitboxes | Frame nào có hitbox enabled thì frame đó active. Đây là mode hiện tại. |
| Activation Mode = AnimationEvents | Bật/tắt window bằng Animation Event. Dùng khi muốn window không phụ thuộc danh sách frame. |
| Frames | Danh sách frame và hitbox của mỗi frame. |
| Frame Index | Frame trong clip. |
| Box Name | Tên debug của box. |
| Enabled | Cho phép box này query. Tắt để giữ dữ liệu nhưng không gây hit. |
| Offset | Tâm box theo local space của attack actor. |
| Size | Kích thước gốc; runtime nhân với `Level.Area Multiplier`. |
| Show Hitboxes In Scene | Chỉ visual debug Editor. Không ảnh hưởng build. |
| Editor Preview Frame/Selected Box | State của editor tool, không phải gameplay. |

#### WeaponAttackActor

| Trường | Ý nghĩa |
|---|---|
| Hitbox Track | Tham chiếu `AnimationHitboxTrack` trên prefab. |
| Animator | Animator phát state. |
| Cleanup Delay | Thời gian chờ thêm sau độ dài clip rồi mới destroy actor. Tăng nếu VFX bị cắt sớm. |

### 8.3 Checklist melee

1. Animator Controller có state đúng tên.
2. AnimationHitboxTrack trỏ đúng clip và Animator.
3. Frame gây damage có hitbox enabled.
4. WeaponAttackActor được gán Track và Animator.
5. Attack Data trỏ prefab trên.
6. WeaponData level trỏ Attack Data.

## 9. Cấu hình Aura/Persistent đầy đủ

Aura dùng `WeaponAttackDefinition` thường với:

- `Lifetime = Persistent`.
- `Attack Prefab` có `WeaponAuraActor`.
- `Hit Cooldown`: khoảng cách giữa hai tick Aura.
- `Hit Limit`: số enemy tối đa mỗi tick.

Prefab Aura cần:

| Component/trường | Ý nghĩa |
|---|---|
| CircleCollider2D | Vùng query gốc. `Is Trigger` nên bật. Radius được nhân với `Area Multiplier`. |
| WeaponAuraActor.Detection Area | Kéo CircleCollider2D vào. |
| WeaponAuraActor.Visual Root | Transform visual được scale cùng Area Multiplier. Có thể null nếu không có visual. |

`Level.Attack Interval` không phải chu kỳ tick chính của Persistent actor; runtime dùng `Attack.Hit Cooldown`.

## 10. Cấu hình projectile đầy đủ

Projectile tách thành ba lớp: visual prefab, `ProjectileDefinition`, `ProjectileWeaponAttackDefinition`.

### 10.1 Projectile visual prefab

| Thành phần | Ý nghĩa |
|---|---|
| WeaponProjectileActor | Bắt buộc. Runtime quản lý pool, đường bay, sweep collision, impact và event hit. Không có trường Inspector. |
| SpriteRenderer/Trail/Particles | Chỉ visual. Sprite nên hướng theo local `+X` vì actor đặt `transform.right` theo hướng bay. |
| Rigidbody2D/Collider2D | Không cần cho projectile mới; actor dùng Raycast/CircleCast chống tunneling. |

### 10.2 ProjectileDefinition: mọi trường

#### Prefab và Movement

| Trường | Ý nghĩa |
|---|---|
| Projectile Prefab | Prefab có `WeaponProjectileActor`. |
| Speed | World unit/giây dọc theo trajectory. Không phải tốc độ bắn. |
| Lifetime | Thời gian sống tối đa. |
| Maximum Range | Tổng quãng đường tối đa và giới hạn endpoint theo chuột. |
| Collision Radius | `0` dùng ray mảnh; lớn hơn 0 dùng CircleCast. Nên gần bán kính visual. |
| Obstacle Layer Mask | Layer môi trường chặn đạn. Không thêm Enemy nếu Enemy đã nằm trong Weapon Target Mask. |

#### Trajectory

| Trường | Ý nghĩa |
|---|---|
| Trajectory = Straight | Bay thẳng theo hướng chụp lúc bắn. |
| Trajectory = Parabolic | Bay theo một đường Bezier từ origin tới endpoint. |
| Randomize Straight Or Curve | Chỉ với Parabolic. Bật để mỗi viên roll thẳng/cong một lần khi spawn. |
| Straight Path Chance | Xác suất chốt đường thẳng. `0.2` là trung bình 20%. |
| Arc Height | Độ lệch control point khi không random height. |
| Randomize Arc Height | Bật để mỗi viên chốt height riêng khi spawn. |
| Minimum Arc Height | Biên dưới random; runtime tự đổi thứ tự nếu min lớn hơn max. |
| Maximum Arc Height | Biên trên random. |
| Arc Side = Left/Right | Tất cả viên cong cùng một phía tương đối hướng bắn. |
| Arc Side = Alternate | Luân phiên trái/phải theo index trong cùng volley. Shot đơn luôn index 0. |
| Arc Side = Random | Mỗi viên chốt trái/phải một lần. Không đổi phía giữa đường. |
| Minimum Aim Distance | Endpoint tối thiểu cách origin, tránh đường cong quá ngắn khi chuột sát nhân vật. |

#### Impact = TargetBounce

| Trường | Ý nghĩa |
|---|---|
| Bounce Radius | Bán kính tìm target kế tiếp quanh điểm hit. |
| Maximum Bounces | Số lần chuyển target sau hit đầu. `3` có thể hit tối đa 4 target. |
| Damage Multiplier Per Bounce | Hệ số theo lũy thừa. `0.8`: 100%, 80%, 64%, 51.2%. |
| Prevent Repeat Targets | Bật để không quay lại target projectile đã hit. |

Sau lần bounce đầu, projectile bay thẳng tới target mới; đường cong ban đầu không được tái tạo cho từng bounce.

#### Impact = PierceAll

| Trường | Ý nghĩa |
|---|---|
| Maximum Pierced Targets | `-1` không giới hạn; `1` kết thúc sau target đầu; `3` tối đa ba target. |
| Damage Multiplier Per Pierce | Hệ số lũy thừa theo số target đã xuyên. `1` giữ nguyên damage. |

#### Impact = ExplodeOnFirstEnemy

| Trường | Ý nghĩa |
|---|---|
| Explosion Radius | Bán kính OverlapCircle tại điểm va chạm enemy đầu tiên. |
| Explosion Damage Multiplier | Multiplier damage cho từng target trong AOE. |
| Maximum Explosion Targets | `-1` không giới hạn; số dương giới hạn số receiver hợp lệ. |

Enemy đầu tiên nằm trong AOE và chỉ nhận một hit explosion, không cộng thêm direct hit.

#### Optional VFX

| Trường | Ý nghĩa |
|---|---|
| Impact Vfx Prefab | Spawn tại mỗi direct impact của Bounce/Pierce hoặc khi đụng obstacle. |
| Explosion Vfx Prefab | Spawn khi ExplodeOnFirstEnemy kích hoạt. |
| Vfx Lifetime | Thời gian tự destroy instance VFX. |

Các trường của impact mode khác vẫn hiện trong Inspector nhưng bị bỏ qua.

### 10.3 ProjectileWeaponAttackDefinition theo level

Mỗi level nên sở hữu một `ProjectileWeaponAttackDefinition` và `ProjectileDefinition` độc lập. Runtime đọc trực tiếp profile hoàn chỉnh của level. Ví dụ level 1 có thể dùng projectile Straight/Pierce, còn level 2 đổi sang Parabolic/ExplodeAtEnd mà không cần thêm override hoặc sửa WeaponData chung.

| Trường | Ý nghĩa |
|---|---|
| Projectile | `ProjectileDefinition` được spawn. |
| Shot Mode = Shot | Luôn resolve thành một projectile; `Projectile Count` bị bỏ qua. |
| Shot Mode = Multishot | Spawn `Projectile Count` viên cùng lúc. |
| Shot Mode = Burst | Spawn `Projectile Count` viên lần lượt, cùng thuộc một activation. |
| Shot Mode = BurstSequence | Lặp `Burst Repeat Count` nhóm Burst trong cùng một activation. |
| Spawn Pattern = Forward | Mọi viên dùng cùng hướng trung tâm. |
| Spawn Pattern = Cone | Phân bố đều từ `-Cone Angle/2` tới `+Cone Angle/2`. |
| Projectile Count | Số viên khi Multishot/Burst; với Burst Sequence là số viên trong mỗi nhóm. |
| Cone Angle | Tổng góc mở; chỉ có tác dụng với Cone và count lớn hơn 1. |
| Burst Shot Interval | Delay cơ sở giữa hai viên trong cùng nhóm Burst/Burst Sequence. Giá trị thực tế được chia cho Attack Speed/Haste. |
| Burst Repeat Count | Tổng số nhóm Burst, tính cả nhóm đầu; chỉ dùng với Burst Sequence. |
| Burst Repeat Interval | Delay từ viên cuối nhóm trước tới viên đầu nhóm sau; chỉ dùng với Burst Sequence và chịu Haste. |
| Per Projectile Damage Multiplier | Multiplier primary damage riêng từng viên. Với 5 viên, `0.2` cho tổng lý thuyết 100% nếu tất cả trúng. |
| Hit Policy = OncePerProjectile | Mỗi viên có tập target riêng; cùng enemy có thể bị nhiều viên trong volley đánh. |
| Hit Policy = OncePerVolley | Toàn volley chia sẻ tập target; mỗi enemy chỉ nhận primary hit từ một viên. |

Thời gian nhả hết một Burst cơ sở là `(Projectile Count - 1) × Burst Shot Interval`. Burst Sequence có tổng thời gian cơ sở bằng `Burst Repeat Count × (Projectile Count - 1) × Burst Shot Interval + (Burst Repeat Count - 1) × Burst Repeat Interval`. Cooldown vũ khí bắt đầu ngay lúc activation, vì vậy đặt Attack Interval lớn hơn tổng thời gian nếu không muốn hai activation chồng nhau.

Prefab `ProjectileAttackActor` chỉ cần `ProjectileWeaponAttackActor`; không thêm sprite/collider vào prefab điều phối này.

## 11. Cấu hình Beam đầy đủ

### 11.1 Beam prefab

Prefab cần:

- `LineRenderer`.
- `BeamWeaponAttackActor` với `Line Renderer` trỏ component trên.

LineRenderer quan trọng:

- `Use World Space`: runtime ép bật.
- `Material`: có thể đặt mặc định; Beam Data có thể override.
- `Sorting Layer/Order`: thứ tự hiển thị.
- `Num Cap Vertices`: đầu tia tròn hơn nếu lớn hơn 0.
- Width và Color trên prefab bị Beam Data ghi đè khi runtime.

### 11.2 BeamWeaponAttackDefinition: mọi trường

| Trường | Ý nghĩa |
|---|---|
| Beam Mode = Pulse | Gây một damage tick rồi chỉ giữ visual tới hết Duration. |
| Beam Mode = Continuous | Gây nhiều tick theo Damage Tick Interval. |
| Aim Tracking = SnapshotOnAttack | Khóa hướng khi kích hoạt. |
| Aim Tracking = FollowCursor | Cập nhật hướng chuột mỗi frame trong thời gian Beam tồn tại. |
| Maximum Range | Chiều dài trước obstacle. |
| Beam Width | Độ dày query damage. `0` Raycast, lớn hơn 0 CircleCast với radius bằng Width/2. |
| Duration | Thời gian actor/visual tồn tại. |
| Damage Tick Interval | Khoảng cách giữa tick Continuous. Pulse bỏ qua sau tick đầu. |
| Damage Multiplier Per Tick | Multiplier damage mỗi tick. |
| Hit Mode = FirstTarget | Dừng sau receiver đầu tiên. |
| Hit Mode = PierceAll | Hit toàn bộ receiver trước obstacle. |
| Hit Mode = LimitedTargets | Hit tối đa Maximum Targets theo thứ tự từ gần đến xa. |
| Maximum Targets | Chỉ có tác dụng với LimitedTargets. |
| OnHit Policy = EveryDamageTick | Mỗi tick accepted có thể proc OnHit. |
| OnHit Policy = OncePerActivation | Damage vẫn tick, nhưng mỗi enemy chỉ proc OnHit ở hit đầu của lần chiếu. OnKill vẫn hoạt động ở tick sau. |
| Obstacle Layer Mask | Layer rút ngắn tia. |
| Start Width/End Width | Độ rộng đầu/cuối LineRenderer. |
| Start Color/End Color | Màu và alpha hai đầu tia. |
| Beam Material | Override material LineRenderer; null dùng material prefab. |
| Muzzle Vfx Prefab | Spawn làm child ở đầu tia trong suốt activation. |
| Impact Vfx Prefab | Spawn ở endpoint và được cập nhật vị trí. |
| Impact Vfx Lifetime | Hiện là dữ liệu dự phòng; runtime persistent impact được destroy cùng Beam thay vì dùng timer này. |

Khuyến nghị Continuous Beam gây status dùng `OncePerActivation` hoặc Internal Cooldown đủ lớn để tránh spam stack.

## 12. Triggered Effects: mọi trường

Trong `WeaponData > Levels > Triggered Effects`:

| Trường | Ý nghĩa |
|---|---|
| Trigger = OnAttack | Chạy một lần mỗi activation, trước khi xét số projectile/target. Context chưa có target. Không dùng effect cần target ở trigger này. |
| Trigger = OnHit | Chạy sau khi receiver chấp nhận damage. |
| Trigger = OnKill | Chạy khi receipt xác nhận hit giết target. |
| Trigger Chance | `1` = 100%, `0.25` = 25%, `0` = không bao giờ. Roll độc lập mỗi lần event hợp lệ. |
| Internal Cooldown | Cooldown riêng của entry trên một WeaponController. Dùng để khống chế Multishot, AOE hoặc Beam. |
| Allow Secondary Hits | Cho entry nhận event từ hit secondary. Mặc định tắt để tránh chain. |
| Effect | Effect data-driven mới. Ưu tiên lựa chọn này. |
| Effect Prefab | GameObject prefab có component kế thừa `WeaponTriggeredEffectActor` ngay trên root, dùng cho effect runtime như Slash Projectile hoặc Explosion. Chỉ dùng khi `Effect` null. Object Picker và thao tác kéo-thả đều chọn prefab GameObject. |
| Generated Hits Can Trigger OnHit | Cho hit do effect tạo ra tiếp tục proc OnHit. Bật có thể tạo chain; hệ thống giới hạn depth 4 nhưng vẫn cần cân bằng. |
| Damage Multiplier | Được effect actor legacy dùng khi tính damage. Data-driven effect có multiplier riêng trong asset và thường không dùng trường này. |

Nếu cả `Effect` và `Effect Prefab` cùng được gán, runtime ưu tiên `Effect`.

## 13. Effect và Status assets

### 13.1 ApplyStatusWeaponEffectDefinition

| Trường | Ý nghĩa |
|---|---|
| Status | Status asset được áp lên `StatusEffectController` của target. Null thì không làm gì. |

Target muốn nhận status phải có `StatusEffectController` trên cùng object với `IDamageReceiver`.

### 13.2 StatusEffectDefinition: trường chung

| Trường | Ý nghĩa |
|---|---|
| Status ID | ID logic. Nếu rỗng dùng tên asset. |
| Duration | Thời gian tồn tại mỗi application. |
| Maximum Stacks | Stack tối đa. |
| Stack Policy = RefreshDuration | Không thêm stack, reset duration. |
| Stack Policy = AddStackAndRefresh | Thêm một stack tới max và reset duration. |
| Stack Policy = ExtendDuration | Không thêm stack, cộng thêm Duration vào thời gian còn lại. |
| Stack Policy = IndependentInstances | Mỗi application tạo runtime instance riêng. |
| Stack Scope = SharedAcrossSources | Tất cả owner dùng chung một stack trên target. |
| Stack Scope = SeparatePerSource | Mỗi owner có stack riêng. |
| Visual Style = None | Không tạo particle. |
| Visual Style = Mark/Slow/Stun/Burn/Poison | Chọn motion preset của particle runtime. |
| Visual Color | Tint particle. |
| Visual Offset | Local position trên target. Y âm ở chân, Y dương trên đầu. |
| Visual Scale | Scale toàn bộ particle visual, không đổi gameplay. |

### 13.3 DamageOverTimeStatusDefinition

| Trường | Ý nghĩa |
|---|---|
| Flat Damage | Damage mỗi stack mỗi tick không phụ thuộc hit ban đầu. |
| Source Damage Multiplier | Phần damage hit đã apply được chụp khi inflict. `0.1` = thêm 10% source damage mỗi stack/tick. |
| Tick Interval | Số giây giữa tick. |
| Tick Immediately | Bật để tick ngay lúc apply; tắt để chờ đủ interval đầu tiên. |
| Element | Physical, Fire, Poison hoặc Arcane, phục vụ resistance/feedback tương lai. |

Damage mỗi tick:

```text
(Flat Damage + Captured Source Damage × Source Damage Multiplier) × Current Stacks
```

DOT không crit và không proc OnHit; DOT vẫn có thể tạo OnKill.

### 13.4 SlowStatusEffectDefinition

| Trường | Ý nghĩa |
|---|---|
| Movement Speed Multiplier | `0.7` còn 70% tốc độ, `0` đứng yên, `1` không slow. Nếu nhiều slow active, runtime lấy multiplier nhỏ nhất. |

### 13.5 StunStatusEffectDefinition

Không có trường riêng. Khi status active, `IsMovementStunned = true`. Movement component phải đọc cờ này; `EnemyFlowFieldAgent` hiện đã hỗ trợ.

### 13.6 MarkStatusEffectDefinition

Mỗi phần tử `Reactions` có:

| Trường | Ý nghĩa |
|---|---|
| Trigger = OnApplied | Chạy mỗi lần mark được apply. |
| Trigger = OnStackChanged | Chạy khi số stack khác trước application. |
| Trigger = OnThresholdReached | Chạy đúng lúc đi từ dưới Required Stacks lên bằng/vượt ngưỡng. |
| Trigger = OnExpired | Chạy khi hết thời gian tự nhiên; không chạy khi dispel/pool reset. |
| Required Stacks | Điều kiện stack tối thiểu. |
| Consume Stacks | Số stack tiêu thụ sau khi chạy effect. 0 không tiêu thụ. |
| Effects | Danh sách effect data-driven chạy theo thứ tự. |

### 13.7 DirectDamageWeaponEffectDefinition

| Trường | Ý nghĩa |
|---|---|
| Flat Damage | Damage cộng cố định. |
| Source Damage Multiplier | Phần source damage. `0.8` = 80%. |
| Element | Loại nguyên tố. |
| Can Trigger OnHit | Cho direct effect hit phát OnHit mới. Mặc định tắt để tránh loop. |

### 13.8 KnockbackWeaponEffectDefinition

| Trường | Ý nghĩa |
|---|---|
| Strength | Độ lớn vận tốc knockback truyền cho `IKnockbackReceiver`. Target cần implement interface này. |

Hướng knockback lấy từ owner tới target. `EnemyFlowFieldAgent.Maximum Knockback Speed` có thể clamp cảm giác đẩy dù Strength lớn hơn.

### 13.9 CompositeWeaponEffectDefinition

| Trường | Ý nghĩa |
|---|---|
| Effects | Danh sách effect chạy tuần tự trên cùng context. Cho phép một OnHit vừa Burn vừa Knockback mà không thêm nhiều entry. |

## 14. Status runtime và prefab Enemy

Enemy muốn dùng đầy đủ OnHit cần:

- Một component triển khai `IDamageReceiver`, hiện là `EnemyHealth`.
- `StatusEffectController` trên cùng GameObject với receiver.
- `StatusEffectVisualController` để thấy particle.
- Movement đọc `MovementSpeedMultiplier` và `IsMovementStunned`.
- Component triển khai `IKnockbackReceiver`; hiện `EnemyFlowFieldAgent` đảm nhiệm.

Các trường liên quan trên Enemy hiện tại:

| Trường | Ý nghĩa |
|---|---|
| EnemyHealth.Max Health | HP được reset trong Awake/pool. |
| EnemyHealth.Current Health | Runtime debug, bị reset về Max Health. |
| EnemyHealth.Log Accepted Hits | Bật để log mọi hit; nên tắt trong production. |
| EnemyFlowFieldAgent.Move Speed | Tốc độ gốc trước Slow/Stun. |
| Acceleration | Tốc độ tiến tới velocity điều hướng. |
| Knockback Recovery | Tốc độ external knockback giảm về 0. Tăng để dừng knockback nhanh hơn. |
| Maximum Knockback Speed | Clamp velocity knockback. Muốn knockback mạnh hơn phải tăng cả Strength lẫn clamp nếu đang chạm trần. |
| Separation Radius/Weight | Tách đàn enemy; không phải thông số weapon. |

## 15. Bonus stat và Area theo level

`WeaponController` cộng dồn `Stat Bonuses` từ level 1 đến level hiện tại rồi đăng ký chúng vào `CharacterCombatSystem` của Player với source `WeaponLevel`. Vì vậy ATK, Critical Hit, Attack Speed, Movement Speed, Pickup Range và Weapon Area đều là stat của Player, không chỉ là giá trị cục bộ của weapon. Các luật Block/Convert toàn cục vẫn được áp dụng sau bước cộng bonus.

Area cuối của một attack/effect được tính như sau:

```text
Final Area Multiplier = Level Area Multiplier × (Player Weapon Area / 100)
```

Runtime dùng kết quả này để scale object attack/effect. Những truy vấn gameplay có kích thước riêng như projectile sweep/explosion/bounce và Beam cast/width cũng nhân đúng cùng hệ số, nên vùng trúng và phần nhìn không bị lệch nhau.

## 16. Prefab effect legacy

### SlashProjectileEffectActor

| Trường/component | Ý nghĩa |
|---|---|
| Speed | Linear velocity của Rigidbody2D. |
| Lifetime | Thời gian tự destroy. |
| Body | Rigidbody2D được điều khiển. |
| Rigidbody2D Body Type | Nên Kinematic, Gravity Scale 0. |
| Collision Detection | Continuous để giảm lọt target ở tốc độ cao. |
| Collider2D Is Trigger | Phải bật để `OnTriggerEnter2D`. |
| Collider Offset/Size | Hình vùng damage; phải khớp visual. |

Projectile legacy này xuyên nhiều target vì không destroy sau hit, nhưng mỗi target chỉ nhận một hit từ instance.

### DeathExplosionEffectActor

| Trường/component | Ý nghĩa |
|---|---|
| Damage Area | CircleCollider2D dùng Overlap query. |
| Collider Radius | Bán kính explosion gameplay. |
| Visual Lifetime | Thời gian prefab tồn tại sau khi apply damage. |
| Log Hits | Bật debug từng target explosion. |

Damage của prefab này dùng `Triggered Effect.Damage Multiplier`.

## 17. Vũ khí content hiện có trong project

### 17.1 Iris Slash melee

Đường dẫn: `GameContent/Weapons/Melee/Weapons/IrisSlashWeapon.asset`.

Thông số chung:

- Base Damage 10.
- Critical Multiplier 1.75.
- Variance 0.
- Affected By Haste bật.
- Target Enemy.
- Attack dùng `SlashAttackDef` và `SlashAttackActor`.

| Level | Interval | Damage | Area | Triggered Effects |
|---|---:|---:|---:|---|
| 1 | 1.5 | 3 | 1 | Slow 100% |
| 2 | 1.0 | 4 | 1.5 | Mark 100%, Poison 20%, Burn 35%, Knockback 100% |
| 3 | 0.9 | 4 | 1.5 | Như level 2 |
| 4 | 0.5 | 8 | 1.5 | Như level 2 + OnAttack Slash Projectile + OnKill Explosion |

Hai hiệu ứng cũ được cấu hình trực tiếp trong Triggered Effects của level 4, không cần trạng thái Awakening:

- `OnAttack` → `SlashProjectilePrefab`, Trigger Chance 100%, Damage Multiplier 0.5.
- Prefab có `SlashProjectileEffectActor`, nên có thể kéo thả vào `Effect Prefab` của Triggered Effect tại bất kỳ level nào.

- Effect Prefab `ExplosionPref`.
- Damage Multiplier hiện là 1000, rất lớn; giảm nếu đây không phải hiệu ứng test.

### 17.2 Fire Zone Aura

Đường dẫn: `GameContent/Weapons/Aura/Weapons/FireZone.asset`.

- Base Damage 1, variance 0, không bị Haste.
- Level Damage 1, Area 1.
- Attack `FireAuraAttack`: Persistent, Hit Cooldown 0.5, Hit Limit -1.
- Prefab radius gốc 5.7; visual root scale theo Area Multiplier.
- Không có Triggered Effect.

`Iris.prefab` hiện gắn `IrisSlashWeapon` ở Starting Level 0. Các giá trị runtime debug đã được trả về mặc định để prefab không khởi động ở level cao.

### 17.3 Triple Arc Bomb ranged

Đường dẫn: `GameContent/Weapons/Ranged/Projectile/Weapons/TripleArcBombWeapon.asset`.

- Base Damage 10, interval 1.5, variance 0, target Enemy.
- Level 1 dùng Burst 2 viên, Forward, Burst Shot Interval 0.15 giây; hai endpoint cùng là vị trí chuột lúc activation.
- Parabolic, random trái/phải và random Arc Height 1.5–4 cho từng viên.
- Speed 9, range 18, lifetime 5, collision radius 0.12.
- `ExplodeAtEnd`: AOE radius 2.5 tại endpoint hoặc nổ sớm nếu gặp obstacle.
- Per Projectile multiplier 0.4, OncePerProjectile; mỗi enemy có thể nhận hai hit AOE ở level 1.
- Explosion visual dùng `TripleArcBombExplosionVfx.prefab` và tự hủy sau 1.2 giây.
- Đã gắn vào child `TripleArcBombWeapon_Test` của `Iris.prefab` để kiểm tra cùng `LookAtMouse`.

Các công thức Shot xuyên, Cone Multishot, Bounce, Rocket, Random Curve, Pulse Beam và Continuous Beam khác nằm trong `Ranged_Weapon_Guide.md`.

## 18. Effect preset hiện có

| Preset | Cấu hình hiện tại |
|---|---|
| BurnStatus | 4s, max 5 stack, AddStackAndRefresh, shared source, 2 flat + 10% source/tick/stack, tick 1s, Fire. |
| PoisonStatus | 6s, max 3 stack, AddStackAndRefresh, separate source, 1 flat + 8% source/tick/stack, tick 0.75s, Poison. |
| SlowStatus | 2s, refresh duration, movement multiplier 0.7. |
| StunStatus | 0.6s, ExtendDuration, movement bị khóa. |
| MarkStatus | 6.12s, max 3 stack; đạt 3 stack chạy MarkBurstDamage rồi consume 3. |
| MarkBurstDamage | 5 flat + 80% source damage, Arcane, không proc OnHit. |
| KnockbackEffect | Strength 5. |

## 19. Checklist tạo một vũ khí mới từ đầu

### Chung

1. Tạo/chọn Attack Actor Prefab đúng delivery.
2. Tạo Attack Definition và gán đúng prefab.
3. Tạo WeaponData, ID duy nhất, Target Layer đúng.
4. Thêm ít nhất một Level, gán Attack.
5. Cấu hình damage/interval/area.
6. Thêm Stat Bonuses theo từng level; nhớ đây là delta cộng dồn, không phải tổng cuối cùng.
7. Thêm Player Stat Rules nếu level cần chặn/chuyển stat từ nguồn khác.
8. Thêm Triggered Effects và kiểm tra ICD.
9. Gắn WeaponData vào WeaponController.
10. Gán Combat Stats, Attack Origin và Aim Provider nếu cần.

### Melee

1. Chuẩn bị sprite, clip và Animator state.
2. Author hitbox theo frame.
3. Kiểm tra state name khớp tuyệt đối.

### Projectile

1. Prefab visual có WeaponProjectileActor.
2. Tạo ProjectileDefinition.
3. Chọn trajectory và impact; chỉ chỉnh nhóm field đúng mode.
4. Tạo Projectile Attack làm template rồi gán vào `Level.Attack`.
5. Trong `Full Level Attack Profiles`, bấm tạo independent copy cho level.
6. Chỉnh toàn bộ Attack và Projectile Configuration của level ngay trong WeaponData Inspector.

### Beam

1. Prefab có LineRenderer và BeamWeaponAttackActor.
2. Tạo Beam Attack, chọn Pulse/Continuous.
3. Chọn Snapshot/FollowCursor, hit mode và OnHit policy.

### Target

1. Layer nằm trong Weapon Target Mask.
2. Có IDamageReceiver và Collider2D.
3. Có StatusEffectController nếu dùng status.
4. Có StatusEffectVisualController nếu cần feedback.
5. Có IKnockbackReceiver nếu dùng Knockback.

## 20. Lỗi cấu hình thường gặp

- Đạn không bắn: Attack Prefab null hoặc prefab không có actor đúng loại.
- Đạn không trúng: Target Layer Mask sai, enemy collider ở child layer khác, hoặc Collision Radius quá nhỏ.
- Projectile bị tường chặn sai: Enemy bị đưa nhầm vào Obstacle Layer Mask.
- Melee có animation nhưng không damage: state/clip/hitbox frame không khớp hoặc target mask sai.
- Aura không tick theo Attack Interval: Persistent dùng Attack Hit Cooldown.
- Burn/Poison không xuất hiện: target thiếu StatusEffectController hoặc ApplyStatus asset chưa trỏ Status.
- Có status nhưng không có particle: thiếu StatusEffectVisualController hoặc Visual Style = None.
- Slow/Stun có status nhưng enemy vẫn chạy: movement chưa đọc StatusEffectController.
- Knockback yếu: tăng Effect Strength, tăng Maximum Knockback Speed hoặc giảm Knockback Recovery.
- Multishot proc quá nhiều: dùng OncePerVolley hoặc tăng Internal Cooldown.
- Continuous Beam stack status quá nhanh: dùng OncePerActivation.
