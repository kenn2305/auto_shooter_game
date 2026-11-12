# Iris — Pixel Art Prompt

**Mô tả:** Tinh linh trắng, nhỏ bé nhưng vô cùng linh hoạt, có đôi cánh trắng.
**Tạo hình:** Sinh vật tròn kiểu Chiikawa (không phải hình người) — đầu tròn to trên thân tròn nhỏ, tay chân ngắn xíu.
**Dáng:** Đứng nghiêng sang phải (góc 3/4), cánh ở sau lưng phía bên trái.
**Kích thước sprite:** 48×48 px — file: `Iris.aseprite`, `Iris_Idle.png`, `Iris_Move.png`

---

## 1. Prompt chính (Character sprite)

```
Pixel art game character sprite, a tiny round white spirit creature named Iris, not human, very round big head sitting on a small round body, tiny stubby arms and feet, body turned three-quarters to the right, simple black dot eyes and a tiny "v" mouth on the right side of the face, soft pink blush, small hair tuft on top, a small blue crescent-moon mark on the forehead, pure white body with pale lavender shading, small feathered white wings on its back, hovering slightly above the ground with a soft shadow, a few silver-blue sparkles, cute yet mysterious mood, thick dark outline, limited pastel palette (white, lavender, periwinkle blue, soft pink), clean readable silhouette, centered
```

## 2. Animation

| Animation | Số khung | Mô tả |
|---|---|---|
| Idle | 4 | Lơ lửng nhấp nhô, cánh vỗ nhẹ, hạt sáng lấp lánh |
| Move | 6 | Bay sang phải, chân quặp ra sau, cánh vỗ nhanh, vệt sáng phía sau |

**Idle**
```
Same character, idle hovering animation, body gently bobbing, small wings flapping softly, sparkles twinkling
```

**Move**
```
Same character, flying to the right, feet tucked back, wings flapping quickly, faint silvery streaks trailing behind
```

## 3. Negative prompt

```
human, humanoid, anime girl, hair, clothes, long limbs, realistic, large detailed anime eyes, scary, blurry, anti-aliasing, noisy dithering, 3D render, text, watermark, multiple characters
```

## 4. Ghi chú

- **Điểm nhận diện:** thân trắng tròn + dấu trăng khuyết xanh trên trán + cánh trắng nhỏ.
- Sprite quay sang phải; đi sang trái thì dùng `flipX` trong Unity.
