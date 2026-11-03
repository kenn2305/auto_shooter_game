# Weapon Content Layout

Thư mục này chỉ chứa content được cấu hình bằng Inspector. Code framework nằm trong `Assets/WeaponSystem`.

- `Effects/Applications`: effect trung gian dùng để gắn một Status vào mục tiêu.
- `Effects/Direct`: effect thực thi ngay như Knockback hoặc direct damage.
- `Effects/Statuses`: dữ liệu Mark, Slow, Stun, Burn và Poison.
- `Melee`: WeaponData, attack data, attack prefab và visual của vũ khí cận chiến.
- `Aura`: WeaponData, attack data và prefab của vùng sát thương Persistent.
- `Ranged/Projectile`: ProjectileDefinition, Shot/Multishot Attack, prefab visual và WeaponData. Preset hiện có là `TripleArcBombWeapon`.
- `Ranged/Beam`: nơi tạo Beam Attack, material/visual và WeaponData; chỉ prefab actor framework được cung cấp sẵn.

`TripleArcBombWeapon` là content kiểm thử có cấu hình đầy đủ, không phải framework bắt buộc. Hãy tạo asset gameplay mới theo `Ranged_Weapon_Guide.md`; không sửa state runtime trực tiếp trên ScriptableObject dùng chung.

Hướng dẫn đầy đủ: `Assets/WeaponSystem/Documentation/Weapon_Configuration_Complete_Guide.md`.

Khi di chuyển asset Unity, luôn di chuyển cùng file `.meta`. Reference dùng GUID nên không phụ thuộc đường dẫn hiện tại.
