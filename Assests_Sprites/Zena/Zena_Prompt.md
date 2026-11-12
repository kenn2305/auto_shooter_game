# Zena — Pixel Art Prompt

**Mô tả:** Tinh linh có 2 dạng.
- **Dạng đen:** hồ ly tinh đen tuyền, mắt đỏ — chỉ thể hiện **3 đuôi** cho gọn (không vẽ đủ 9).
- **Dạng trắng:** mèo trắng, mắt đen.

**Tạo hình:** Sinh vật tròn kiểu Chiikawa (không phải hình người) — đầu tròn to, thân tròn nhỏ, tay chân ngắn xíu.
**Dáng:** Đứng nghiêng sang phải (góc 3/4), đuôi chìa ra phía sau bên trái.
**Kích thước sprite:** 48×48 px — file: `Zena_Dark.*`, `Zena_White.*` (cùng khung dáng để chuyển dạng liền mạch)

---

## 1. Dạng đen — Hồ ly

```
Pixel art game character sprite, a small round black fox spirit creature named Zena (dark form), not human, very round big head on a small round body, tiny stubby arms and feet, body turned three-quarters to the right, two tall pointed fox ears with dark violet insides, small narrowed glowing crimson eyes, faint dark blush, tiny smile, small crimson crescent mark on the forehead, jet-black fur with deep purple shading and a thin violet rim light, three big fluffy black tails sticking out behind her to the left, each tail tip glowing with a small red-violet spirit flame, a few dark-red embers floating, cute but mysterious mood, thick dark outline, limited palette (black, deep purple, crimson, violet glow), centered
```

## 2. Dạng trắng — Mèo

```
Pixel art game character sprite, a small round white cat spirit creature named Zena (light form), not human, very round big head on a small round body, tiny stubby arms and feet, body turned three-quarters to the right, small pointed cat ears with pink insides, simple black dot eyes with a tiny white glint, "w" cat mouth, soft pink blush, small dark crescent mark on the forehead, pure white fur with pale blue-gray shading, one long white tail curling up behind her on the left, a few pale-gold sparkles, sweet and gentle yet mysterious mood, thick dark outline, limited pastel palette (white, blue-gray, soft pink, pale gold), centered
```

## 3. Animation

| Animation | Số khung | Dạng đen | Dạng trắng |
|---|---|---|---|
| Idle | 4 | Thở nhẹ, 3 đuôi đung đưa lệch pha, lửa đầu đuôi bập bùng | Thở nhẹ, đuôi cong vẫy, tai giật giật |
| Move | 6 | Bước lạch bạch sang phải, đuôi vuốt ra sau, tai cụp nhẹ | Bước lạch bạch sang phải, đuôi lắc theo nhịp |

**Idle**
```
Same character, idle breathing animation, head bobbing 1px, tails swaying gently out of sync, tail-tip flames flickering (dark form) / ear twitching (light form)
```

**Move**
```
Same character, waddling walk to the right, tiny feet stepping, body bouncing, tails streaming back behind her
```

## 4. Negative prompt

```
human, humanoid, anime girl, hair, clothes, long limbs, realistic animal anatomy, long snout, too many tails, scary, gore, blurry, anti-aliasing, noisy dithering, 3D render, text, watermark, multiple characters
```

## 5. Ghi chú

- **Liên kết 2 dạng:** cùng khung thân + dấu trăng khuyết trên trán (đỏ ở dạng đen, sẫm ở dạng trắng).
- Sprite quay sang phải; đi sang trái thì dùng `flipX` trong Unity.
