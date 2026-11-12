# Lily — Pixel Art Prompt

**Mô tả:** Tinh linh phù thủy trùm kín người trong áo choàng tím có mũ trùm. Khuôn mặt chìm trong bóng tối, chỉ thấy **hai con mắt xanh phát sáng**. Cây gậy phép **bay lơ lửng** bên cạnh, Lily không cầm.
**Tạo hình:** Sinh vật tròn kiểu Chiikawa (không phải hình người) — đầu tròn to, thân tròn nhỏ, tất cả bọc trong áo choàng.
**Dáng:** Đứng nghiêng sang phải (góc 3/4), chóp mũ trùm rủ ra sau bên trái.
**Kích thước sprite:** 48×48 px — file: `Lily.aseprite`, `Lily_Idle.png`, `Lily_Move.png`

---

## 1. Prompt chính (Character sprite)

```
Pixel art game character sprite, a small round spirit witch creature named Lily, not human, completely covered by a purple hooded cloak, very round big hooded head on a small round robed body, body turned three-quarters to the right, the hood's pointed tip drooping back to the left, the face hidden in deep shadow inside the hood with only two glowing blue eyes visible, gold trim on the cloak hem and sleeve cuff, tiny sleeve nub with no visible hands, a wooden magic staff with a glowing light-blue crystal floating upright beside her (not held), small blue sparkles around the crystal, cute yet mysterious mood, thick dark outline, limited palette (purple, deep violet, gold, glowing blue, near-black), centered
```

## 2. Animation

| Animation | Số khung | Mô tả |
|---|---|---|
| Idle | 4 | Thở nhẹ, chóp mũ trùm rủ theo trễ 1 khung, mắt chớp, gậy bay nhấp nhô lệch nhịp, pha lê sáng nhấp nháy |
| Move | 6 | Bước lạch bạch sang phải (lộ mũi chân dưới vạt áo), gậy bay theo và nghiêng nhẹ |

**Idle**
```
Same character, idle animation, body bobbing 1px, hood tip swaying with a slight delay, glowing eyes blinking slowly, floating staff bobbing out of sync, crystal pulsing
```

**Move**
```
Same character, waddling walk to the right, little feet peeking under the cloak hem, floating staff following beside her and tilting slightly
```

## 3. Negative prompt

```
human, humanoid, visible face, visible mouth, visible hands, holding the staff, realistic, tall body, scary, blurry, anti-aliasing, noisy dithering, 3D render, text, watermark, multiple characters
```

## 4. Ghi chú

- **Điểm nhận diện:** áo choàng tím trùm kín + bóng tối trong mũ trùm với 2 mắt xanh + cây gậy bay.
- Sprite quay sang phải; đi sang trái thì dùng `flipX` trong Unity.
