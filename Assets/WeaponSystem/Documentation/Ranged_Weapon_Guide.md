# Ranged Weapon System - Hướng dẫn cấu hình

Hệ thống ranged mở rộng `WeaponController` bằng hai kiểu delivery:

- `ProjectileWeaponAttackActor`: Shot, Multishot, Burst và Burst Sequence; Straight/Parabolic; Bounce/Pierce/Explosion.
- `BeamWeaponAttackActor`: Pulse Beam và Continuous Beam.

Mọi hit hợp lệ đều trả `WeaponHitResult` về `WeaponController`, nên OnHit, OnKill, Burn, Poison, Mark, Slow, Stun và Knockback hoạt động giống melee.

Project không chứa WeaponData, AttackDefinition, ProjectileDefinition, visual hoặc material ranged dựng sẵn. Các thư mục `Definitions`, `Attacks` và `Weapons` được giữ lại làm nơi author content. Hai prefab framework được giữ lại:

- `Assets/GameContent/Weapons/Ranged/Projectile/Prefabs/ProjectileAttackActor.prefab`
- `Assets/GameContent/Weapons/Ranged/Beam/Prefabs/BeamAttackActor.prefab`

## 1. Chuỗi asset cần tạo

Projectile:

```text
WeaponData
  → ProjectileWeaponAttackDefinition
    → ProjectileAttackActor.prefab
    → ProjectileDefinition
      → prefab hình viên đạn có WeaponProjectileActor
```

Beam:

```text
WeaponData
  → BeamWeaponAttackDefinition
    → BeamAttackActor.prefab
```

`WeaponData`, AttackDefinition và ProjectileDefinition là dữ liệu dùng chung. Actor/prefab chứa state của từng lần bắn ở runtime.

## 2. Tạo prefab hình viên đạn

Tạo một GameObject mới, thêm:

- `WeaponProjectileActor`: runtime bắt buộc; di chuyển, sweep va chạm, damage và báo hit về controller.
- `SpriteRenderer`, `TrailRenderer`, Particle System hoặc visual child tùy ý.
- Cho visual nhìn theo local `+X`, vì runtime đặt `transform.right` theo tiếp tuyến đường bay.

Không cần Rigidbody2D hoặc Collider2D; runtime dùng query sweep để hạn chế xuyên mục tiêu ở tốc độ cao.

Prefab này không chứa damage, số viên, góc hình nón, impact mode hoặc OnHit. Lưu prefab riêng vào `Ranged/Projectile/Prefabs`.

## 3. ProjectileDefinition

Tạo bằng:

`Create > Weapon > Projectile > Definition`

### Trường chung

| Trường | Công dụng |
|---|---|
| Projectile Prefab | Prefab hình viên đạn có `WeaponProjectileActor`. |
| Speed | Tốc độ di chuyển, world unit/giây. |
| Lifetime | Thời gian tồn tại tối đa. |
| Maximum Range | Tổng quãng đường tối đa; điểm chuột cũng bị clamp theo giới hạn này. |
| Collision Radius | 0 dùng ray mảnh; lớn hơn 0 dùng CircleCast. |
| Obstacle Layer Mask | Layer môi trường chặn đạn; không đưa Enemy vào đây. |
| Trajectory | `Straight` hoặc `Parabolic`. |
| Impact Mode | `TargetBounce`, `PierceAll`, `ExplodeOnFirstEnemy` hoặc `ExplodeAtEnd`. |

Target được lấy từ `WeaponData.Target Layer Mask`.

### TargetBounce

| Trường | Công dụng |
|---|---|
| Bounce Radius | Bán kính tìm enemy kế tiếp quanh mục tiêu vừa trúng. |
| Maximum Bounces | Số lần chuyển sang mục tiêu mới sau hit đầu. |
| Damage Multiplier Per Bounce | Hệ số damage nối tiếp; 0.8 cho 100%, 80%, 64%... |
| Prevent Repeat Targets | Không quay lại enemy đã trúng. |

### PierceAll

| Trường | Công dụng |
|---|---|
| Maximum Pierced Targets | `-1` là không giới hạn; số dương là số mục tiêu tối đa. |
| Damage Multiplier Per Pierce | Hệ số damage sau mỗi mục tiêu. Đặt 1 để không giảm. |

### ExplodeOnFirstEnemy

| Trường | Công dụng |
|---|---|
| Explosion Radius | Bán kính AOE tại enemy đầu tiên. |
| Explosion Damage Multiplier | Hệ số damage áp lên từng enemy trong vùng. |
| Maximum Explosion Targets | `-1` là không giới hạn. |

Enemy va chạm đầu tiên nằm trong truy vấn AOE và chỉ nhận damage một lần.

### ExplodeAtEnd

- Projectile không gây direct hit khi lướt qua enemy.
- Projectile nổ và chạy truy vấn AOE khi tới endpoint đã chụp từ con trỏ chuột.
- Nếu chạm layer trong `Obstacle Layer Mask` trước endpoint, projectile nổ sớm tại vật cản.
- Các trường `Explosion Radius`, `Explosion Damage Multiplier` và `Maximum Explosion Targets` dùng giống `ExplodeOnFirstEnemy`.
- Dùng `Spawn Pattern = Forward` nếu nhiều projectile phải có cùng endpoint; `Cone` sẽ xoay hướng và tạo endpoint khác nhau.

### Parabolic và random đường bay

| Trường | Công dụng |
|---|---|
| Randomize Straight Or Curve | Mỗi viên roll một lần lúc spawn để chọn thẳng hoặc cong. |
| Straight Path Chance | Xác suất đi thẳng khi random được bật; 0–1. |
| Arc Height | Độ cong cố định khi không random chiều cao. |
| Randomize Arc Height | Mỗi viên chốt một Arc Height riêng. |
| Minimum/Maximum Arc Height | Khoảng random độ cong. |
| Arc Side Left/Right | Cong cố định về một phía tương đối với hướng bắn. |
| Arc Side Alternate | Viên chẵn/lẻ đổi trái/phải trong cùng volley. |
| Arc Side Random | Mỗi viên chốt trái hoặc phải một lần lúc spawn. |
| Minimum Aim Distance | Không cho endpoint quá gần nòng súng. |
| Impact VFX Prefab | VFX sinh tại điểm va chạm thường. |
| Explosion VFX Prefab | VFX sinh khi impact dạng Explosion kích hoạt. |
| VFX Lifetime | Thời gian tự hủy VFX được spawn. |

Mỗi viên chốt `Trajectory`, `Arc Side` và `Arc Height` khi spawn, không đổi phía giữa đường. Đường cong là một Bezier liên tục tới điểm chuột đã chụp.

Gợi ý:

| Kết quả | Thiết lập |
|---|---|
| Cong cố định | Parabolic; hai Randomize tắt; Arc Side Left/Right. |
| Cong trái/phải ngẫu nhiên | Parabolic; Straight Chance 0; Arc Side Random. |
| Độ cong khác nhau | Randomize Arc Height bật và đặt Min/Max. |
| Trộn thẳng và cong | Randomize Straight Or Curve bật; Chance khoảng 0.15–0.35. |
| Volley hỗn hợp | Hai Randomize bật; Chance 0.2; Arc Side Random; Height 0.75–3.25. |

## 4. ProjectileWeaponAttackDefinition

Tạo bằng:

`Create > Weapon > Attack > Projectile`

| Trường | Công dụng |
|---|---|
| Attack ID / Display Name | Định danh ổn định và tên đọc được. |
| Lifetime | Dùng `Transient`. |
| Attack Prefab | Gán `ProjectileAttackActor.prefab`, không phải prefab hình viên đạn. |
| Animator State Name | Chỉ dùng nếu workflow animation cần state tương ứng. |
| Hit Rule / Hit Limit / Hit Cooldown | Trường nền legacy; projectile quản lý hit bằng Hit Policy và state riêng. |
| Projectile | ProjectileDefinition vừa tạo. |
| Shot Mode | `Shot` luôn một viên; `Multishot` spawn toàn bộ cùng lúc; `Burst` bắn lần lượt một nhóm; `BurstSequence` lặp lại nhiều nhóm Burst trong cùng activation. |
| Spawn Pattern Forward | Các viên cùng hướng tới chuột. |
| Spawn Pattern Cone | Chia đều các hướng trong hình nón quanh hướng tới chuột. |
| Projectile Count | Số viên của Multishot/Burst; với Burst Sequence đây là số viên trong mỗi nhóm. |
| Cone Angle | Tổng góc từ viên ngoài trái đến ngoài phải. |
| Burst Shot Interval | Khoảng trễ cơ sở giữa hai viên trong cùng nhóm Burst/Burst Sequence. Haste rút ngắn delay. |
| Burst Repeat Count | Tổng số nhóm, tính cả nhóm đầu tiên; chỉ dùng bởi Burst Sequence. |
| Burst Repeat Interval | Khoảng nghỉ từ viên cuối nhóm trước đến viên đầu nhóm sau; chỉ dùng bởi Burst Sequence và được Haste rút ngắn. |
| Per Projectile Damage Multiplier | Hệ số damage của từng viên. |
| Hit Policy OncePerProjectile | Mỗi viên có thể hit cùng enemy. |
| Hit Policy OncePerVolley | Cả loạt chỉ hit mỗi enemy một lần. |

Với 5 viên, bắt đầu từ multiplier 0.2 nếu muốn tổng damage lý tưởng bằng một hit. Tăng lên nếu Multishot cần mạnh hơn Shot.

Burst và Burst Sequence khóa aim một lần khi activation bắt đầu. Tổng thời gian cơ sở của Burst là `(Projectile Count - 1) × Burst Shot Interval`. Với Burst Sequence, tổng thời gian là `Burst Repeat Count × (Projectile Count - 1) × Burst Shot Interval + (Burst Repeat Count - 1) × Burst Repeat Interval`. Nếu không muốn hai activation chồng nhau, đặt Attack Interval lớn hơn tổng thời gian sau Haste.

Ví dụ bắn 3 viên liên tiếp, nghỉ 0.8 giây rồi lặp lại tổng cộng 4 nhóm: `Shot Mode = BurstSequence`, `Projectile Count = 3`, `Burst Shot Interval = 0.15`, `Burst Repeat Count = 4`, `Burst Repeat Interval = 0.8`. Tổng cộng bắn 12 projectile; `OnAttack` chạy một lần, còn `OnHit` được xét riêng theo từng hit hợp lệ.

## 5. BeamWeaponAttackDefinition

Prefab framework đã có `LineRenderer` và `BeamWeaponAttackActor`:

`Assets/GameContent/Weapons/Ranged/Beam/Prefabs/BeamAttackActor.prefab`

Prefab không gán material mặc định. Hãy tạo material visual riêng và gán vào `Beam Material` của BeamAttackDefinition.

Tạo asset bằng:

`Create > Weapon > Attack > Beam`

### Trường chung

| Trường | Công dụng |
|---|---|
| Lifetime | Dùng `Transient`. |
| Attack Prefab | Gán `BeamAttackActor.prefab`. |
| Maximum Range | Chiều dài tối đa. |
| Beam Width | 0 dùng Raycast; lớn hơn 0 dùng CircleCast. |
| Beam Material | Material LineRenderer của beam này. |
| Start Width / End Width | Độ rộng visual của LineRenderer ở đầu/cuối tia; độc lập với Beam Width dùng cho va chạm. |
| Start Color / End Color | Màu visual ở đầu và cuối tia. |
| Muzzle VFX Prefab | VFX sinh tại nòng khi beam hoạt động. |
| Impact VFX Prefab | VFX sinh tại điểm cuối/hit của beam. |
| Impact VFX Lifetime | Thời gian tự hủy impact VFX. |
| Obstacle Layer Mask | Layer chặn tia. |

### Pulse Beam

- `Beam Mode = Pulse`.
- `Aim Tracking = SnapshotOnAttack`.
- `Duration = 0.1–0.2`.
- Damage xảy ra một lần.

### Continuous Beam

- `Beam Mode = Continuous`.
- `Aim Tracking = FollowCursor` nếu muốn tia bám chuột.
- `Duration`: thời gian duy trì của một activation.
- `Damage Tick Interval`: thời gian giữa hai tick damage.
- `Damage Multiplier Per Tick`: hệ số damage của mỗi tick.

### Hit Mode và OnHit Policy

- `FirstTarget`: chỉ enemy đầu tiên.
- `PierceAll`: tất cả enemy trước obstacle.
- `LimitedTargets`: tối đa `Maximum Targets`.
- `EveryDamageTick`: mỗi tick có thể kích hoạt Triggered Effect.
- `OncePerActivation`: damage vẫn tick, nhưng OnHit chỉ chạy lần đầu trên mỗi enemy trong activation.

Continuous Beam thường nên dùng `OncePerActivation` để không spam Burn, Stun hoặc Knockback.

## 6. WeaponData

Tạo bằng:

`Create > Weapon > Weapon Data`

### Trường gốc

| Trường | Công dụng |
|---|---|
| Weapon ID | ID ổn định, duy nhất. |
| Display Name / Icon | Dữ liệu UI. |
| Base Damage | Damage gốc. |
| Critical Hit Multiplier | Hệ số khi crit. |
| Damage Variance Percent | Biên độ ngẫu nhiên; đặt 0 khi test. |
| Affected By Haste | Cho Haste giảm Attack Interval. |
| Target Layer Mask | Layer có thể nhận hit, thường là Enemy. |
| Levels | Mỗi level tự cấu hình stat bonus, attack và Triggered Effects; không có hệ thống Awakening riêng. |

### Mỗi phần tử Levels

| Trường | Công dụng |
|---|---|
| Description | Mô tả level. |
| Attack Interval | Thời gian giữa hai activation. |
| Minimum Attack Interval | Sàn sau Haste; `-1` là không giới hạn riêng. |
| Damage | Multiplier damage của level. |
| Area Multiplier | Hệ số kích thước cho attack/effect có hỗ trợ. |
| Stat Bonuses | Bonus owner được cấp ở level này và cộng dồn với các level trước; `30` là +30 percentage points. |
| Player Stat Rules | Rule Block/Convert áp dụng lên stat cuối của player từ level này trở đi; có thể miễn stat thuộc chính weapon bằng owner key. |
| Attack | Profile Projectile hoặc Beam AttackDefinition hoàn chỉnh của level. |
| Triggered Effects | Danh sách OnAttack, OnHit, OnKill. |

Trong `Full Level Attack Profiles`, tạo independent copy cho level trước khi chỉnh. Projectile attack sẽ clone luôn `ProjectileDefinition`, nên có thể thay mode, số viên, trajectory, impact, explosion và VFX mà không tác động level khác.

Projectile cơ sở:

`Base Damage × Level Damage × Per Projectile Damage Multiplier`

`Attack Interval` là tốc độ bắn; `Projectile Speed` là tốc độ bay; `Beam Damage Tick Interval` là tốc độ damage bên trong một lần chiếu.

## 7. Triggered Effects

Trong `WeaponData > Levels > Triggered Effects`:

| Trường | Công dụng |
|---|---|
| Trigger | `OnAttack`, `OnHit` hoặc `OnKill`. |
| Trigger Chance | 1 = 100%, 0.25 = 25%. |
| Internal Cooldown | Cooldown riêng của entry trên WeaponController. |
| Allow Secondary Hits | Cho damage thứ cấp kích hoạt entry này. Mặc định nên tắt. |
| Effect | Asset ApplyBurn, ApplyPoison, ApplySlow, ApplyStun, ApplyMark, Knockback... |
| Effect Prefab | GameObject prefab có `WeaponTriggeredEffectActor` trên root; để trống với effect data-driven. |
| Generated Hits Can Trigger OnHit | Cho hit do effect sinh ra proc OnHit; tránh bật nếu có nguy cơ vòng lặp. |

Sự kiện:

- `OnAttack`: một lần mỗi activation, không phải mỗi viên.
- `OnHit`: mỗi accepted hit. Bounce, Pierce và Explosion có thể tạo nhiều hit.
- `OnKill`: mỗi enemy chết bởi hit tương ứng.

ICD gợi ý:

- Shot đơn: 0.
- Multishot: 0.1–0.2 giây.
- Explosion: 0.1 giây nếu không muốn mọi enemy AOE proc.
- Continuous + EveryDamageTick: bằng hoặc lớn hơn Damage Tick Interval.
- Continuous + OncePerActivation: có thể để 0.

## 8. Gắn vào prefab nhân vật

Trên child vũ khí có `WeaponController`:

| Trường | Cấu hình |
|---|---|
| Weapon Data | WeaponData vừa tạo. |
| Starting Level | Index bắt đầu; level đầu là 0. |
| Combat Stats | `CharacterCombatSystem` của chủ sở hữu. |
| Attack Origin | Transform nòng súng; để trống dùng transform controller. |
| Aim Provider Behaviour | Kéo `LookAtMouse`; để trống sẽ tìm provider ở parent. |
| Auto Attack | Tự bắn khi cooldown hết. |
| Attack Immediately | Bắn ngay lúc enable. |

Projectile lấy điểm chuột lúc activation. Projectile cong giữ endpoint đã chụp. Continuous Beam với FollowCursor có thể cập nhật hướng theo chuột trong thời gian duy trì.

Enemy cần:

- Nằm trong `Target Layer Mask`.
- Có Collider2D.
- Có component triển khai `IDamageReceiver`.
- Có `StatusEffectController` nếu nhận status.
- Có `StatusEffectVisualController` nếu cần particle feedback.
- Có `IKnockbackReceiver` nếu nhận Knockback.

## 9. Công thức cấu hình nhanh

### Shot xuyên thẳng

- Projectile: Straight, PierceAll, max pierced `-1`, multiplier 1.
- Attack: Shot, Forward, per-projectile multiplier 1.
- Triggered Effect tùy chọn: OnHit Knockback.

### Multishot hình nón

- Projectile: Straight, PierceAll hoặc giới hạn target tùy balance.
- Attack: Multishot, Cone, 5 viên, 40 độ, multiplier 0.2–0.35.
- Dùng OncePerVolley nếu không muốn cùng enemy ăn nhiều viên.

### Đạn bật nảy

- Projectile: Straight, TargetBounce, radius 4, maximum bounces 3, multiplier 0.8, Prevent Repeat bật.
- Attack: Shot, Forward.
- Triggered Effect tùy chọn: OnHit Slow.

### Rocket cong phát nổ

- Projectile: Parabolic, Arc Height 2.5, Arc Side Left/Right/Random; ExplodeOnFirstEnemy, radius 2.5.
- Attack: Shot, Forward.
- Triggered Effect tùy chọn: OnHit Burn; ICD khoảng 0.1.

### Random Curve Multishot

- Projectile: Parabolic; random Straight/Curve bật, Chance 0.2; random Height 0.75–3.25; Arc Side Random.
- Impact: PierceAll; Maximum Pierced Targets 1 nếu muốn mất sau enemy đầu.
- Attack: Multishot, Cone, 7 viên, 18 độ, multiplier 0.25, OncePerProjectile.
- Mỗi viên tự chốt một đường bay lúc spawn và giữ nguyên đến cuối.

### Triple Arc Bomb đã cấu hình trong project

Các asset:

- `Ranged/Projectile/Weapons/TripleArcBombWeapon.asset`
- `Ranged/Projectile/Attacks/TripleArcBombAttack.asset`
- `Ranged/Projectile/Definitions/TripleArcBombProjectile.asset`
- `Ranged/Projectile/Prefabs/TripleArcBombProjectile.prefab`
- `Ranged/Projectile/Prefabs/TripleArcBombExplosionVfx.prefab`

Cấu hình chính:

- Level 1: Burst 2 viên, `Burst Shot Interval = 0.15`, `Spawn Pattern = Forward`; hai viên cùng tới điểm chuột đã chụp khi activation bắt đầu.
- Parabolic, `Arc Side = Random`, random Arc Height 1.5–4; mỗi viên giữ đường đã chốt.
- `Impact Mode = ExplodeAtEnd`, AOE radius 2.5, không giới hạn target.
- Mỗi projectile dùng multiplier 0.4 và `OncePerProjectile`, nên ở level 1 một enemy trong vùng có thể nhận hai lần AOE.
- Speed 9, Maximum Range 18, Lifetime 5, obstacle layer `Ostacle`.
- Weapon Base Damage 10, Attack Interval 1.5, Variance 0.
- `Iris.prefab` có child `TripleArcBombWeapon_Test` để chạy thử; xóa hoặc disable child này nếu không muốn trang bị preset.

### Pulse Beam xuyên mục tiêu

- Pulse, SnapshotOnAttack, Duration 0.15.
- Range 14, width 0.12, PierceAll.
- EveryDamageTick; Pulse chỉ gây một tick.
- Triggered Effect tùy chọn: OnHit Knockback.

### Continuous Beam bám chuột

- Continuous, FollowCursor, Duration 2.
- Range 9, width 0.25; tick 0.2; multiplier mỗi tick 0.2.
- FirstTarget hoặc PierceAll theo thiết kế.
- OncePerActivation nếu có Burn/Poison/Knockback.

## 10. Checklist kiểm tra

1. AttackDefinition đã gán đúng prefab actor framework.
2. ProjectileDefinition đã gán prefab visual có `WeaponProjectileActor`.
3. BeamDefinition đã gán material nếu cần hiển thị.
4. WeaponData có ít nhất một Level và Attack.
5. Target Layer Mask gồm layer enemy.
6. Obstacle Layer Mask không chứa layer enemy.
7. WeaponController có WeaponData, Combat Stats và aim provider hợp lệ.
8. Target có receiver/collider/status components cần thiết.
9. Test với Variance = 0 trước khi cân bằng.
10. Kiểm tra Console không có Missing Script hoặc Missing Asset.

