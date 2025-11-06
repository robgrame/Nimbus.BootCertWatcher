# ?? Dashboard Image & Logo Guidelines

## ?? Dimensioni Raccomandate

### 1. Navbar Logo

**Posizione:** Top navigation bar (sempre visibile)

| Formato | Altezza | Larghezza | Aspect Ratio | Peso File |
|---------|---------|-----------|--------------|-----------|
| **SVG** (Raccomandato) | 32px | Auto (max 150px) | 1:1 o 16:9 | < 10KB |
| **PNG** | 32px (64px @2x) | Auto (max 150px) | 1:1 o 16:9 | < 20KB |
| **WebP** | 32px (64px @2x) | Auto (max 150px) | 1:1 o 16:9 | < 15KB |

**Responsive:**
- **Mobile** (<576px): 28px height
- **Tablet** (?576px): 32px height
- **Desktop** (?992px): 36px height

### 2. Hero Banner (Homepage)

**Posizione:** Under navbar, top of homepage

| Device | Width | Height | Aspect Ratio | Peso File |
|--------|-------|--------|--------------|-----------|
| **Desktop** | 1920px | 400-600px | 16:9 o 21:9 | < 200KB |
| **Tablet** | 1200px | 300-400px | 16:9 | < 150KB |
| **Mobile** | 768px | 200-300px | 4:3 o 16:9 | < 100KB |

**Formato Raccomandato:** WebP con fallback JPEG

### 3. Card/Thumbnail Images

**Posizione:** Device cards, report previews

| Tipo | Dimensione | Aspect Ratio | Peso File |
|------|-----------|--------------|-----------|
| **Small** | 150x150px | 1:1 (square) | < 30KB |
| **Medium** | 300x200px | 3:2 | < 50KB |
| **Large** | 600x400px | 3:2 | < 100KB |

### 4. Favicon

**Posizione:** Browser tab, bookmarks

| Formato | Dimensione | Trasparenza |
|---------|-----------|-------------|
| **ICO** | 16x16, 32x32, 48x48 | No |
| **PNG** | 192x192, 512x512 | Sì |
| **SVG** | Scalable | Sì |

---

## ?? Struttura File Consigliata

```
SecureBootDashboard.Web/
??? wwwroot/
?   ??? images/
?   ?   ??? logo/
?   ?   ?   ??? logo.svg              (32x32 - Navbar principale)
?   ?   ?   ??? logo-dark.svg         (32x32 - Tema dark)
?   ?   ?   ??? logo-light.svg        (32x32 - Tema light)
?   ?   ?   ??? logo-large.png        (512x512 - Email, print)
?   ?   ?   ??? logo-icon.png         (128x128 - PWA icon)
?   ?   ??? hero/
?   ?   ?   ??? hero-desktop.webp     (1920x500)
?   ?   ?   ??? hero-tablet.webp      (1200x400)
?   ?   ?   ??? hero-mobile.webp      (768x300)
?   ?   ?   ??? hero-fallback.jpg     (1920x500 - Fallback)
?   ?   ??? icons/
?   ?   ?   ??? secure-boot.svg       (64x64)
?   ?   ?   ??? certificate.svg       (64x64)
?   ?   ?   ??? device.svg            (64x64)
?   ?   ??? devices/
?   ?       ??? placeholder.png       (300x200)
?   ??? favicon/
?       ??? favicon.ico               (Multiple sizes)
?       ??? favicon-16x16.png
?       ??? favicon-32x32.png
?       ??? android-chrome-192x192.png
?       ??? android-chrome-512x512.png
?       ??? apple-touch-icon.png      (180x180)
```

---

## ?? Implementazione Logo Navbar

### Opzione 1: SVG Logo (Raccomandato)

```html
<a class="navbar-brand" asp-area="" asp-page="/Index">
    <img src="~/images/logo/logo.svg" 
         alt="Secure Boot Dashboard" 
         class="navbar-brand-logo">
    <span class="ms-2">Secure Boot Dashboard</span>
</a>
```

**CSS:**
```css
.navbar-brand-logo {
    height: 32px;
    width: auto;
    margin-right: 0.5rem;
    vertical-align: middle;
}

@media (max-width: 576px) {
    .navbar-brand-logo {
        height: 28px;
    }
}
```

### Opzione 2: Retina-Ready PNG Logo

```html
<a class="navbar-brand" asp-area="" asp-page="/Index">
    <img src="~/images/logo/logo.png" 
         srcset="~/images/logo/logo.png 1x, ~/images/logo/logo@2x.png 2x"
         alt="Secure Boot Dashboard" 
         class="navbar-brand-logo">
    <span class="ms-2">Secure Boot Dashboard</span>
</a>
```

### Opzione 3: Logo con Tema Dark/Light

```html
<a class="navbar-brand" asp-area="" asp-page="/Index">
    <img src="~/images/logo/logo-light.svg" 
         alt="Secure Boot Dashboard" 
         class="navbar-brand-logo d-none d-dark-mode">
    <img src="~/images/logo/logo-dark.svg" 
         alt="Secure Boot Dashboard" 
         class="navbar-brand-logo d-dark-mode">
    <span class="ms-2">Secure Boot Dashboard</span>
</a>
```

---

## ??? Implementazione Hero Banner

### Responsive Hero Banner con WebP

```html
<div class="hero-banner mb-4">
    <picture>
        <!-- WebP format for modern browsers -->
        <source media="(min-width: 1200px)" srcset="~/images/hero/hero-desktop.webp" type="image/webp">
        <source media="(min-width: 768px)" srcset="~/images/hero/hero-tablet.webp" type="image/webp">
        <source media="(max-width: 767px)" srcset="~/images/hero/hero-mobile.webp" type="image/webp">
        
        <!-- JPEG fallback -->
        <img src="~/images/hero/hero-fallback.jpg" 
             alt="Secure Boot Dashboard" 
             class="img-fluid w-100"
             loading="lazy">
    </picture>
</div>
```

**CSS:**
```css
.hero-banner {
    max-height: 500px;
    overflow: hidden;
    border-radius: 0.5rem;
    box-shadow: 0 0.5rem 1rem rgba(0, 0, 0, 0.15);
}

.hero-banner img {
    object-fit: cover;
    width: 100%;
    height: auto;
}
```

---

## ?? Tool per Creare/Ottimizzare Immagini

### Online Tools (Gratuiti)

| Tool | Uso | Link |
|------|-----|------|
| **Figma** | Design logo/graphics | https://www.figma.com |
| **Canva** | Quick logo creation | https://www.canva.com |
| **Logo Maker** | AI logo generator | https://www.logomaker.com |
| **SVG Optimizer (SVGO)** | Compress SVG | https://jakearchibald.github.io/svgomg/ |
| **Squoosh** | Image compression | https://squoosh.app |
| **TinyPNG** | PNG/JPEG compression | https://tinypng.com |
| **CloudConvert** | Format conversion | https://cloudconvert.com |
| **Favicon Generator** | Generate all favicon sizes | https://realfavicongenerator.net |

### Command Line Tools

```bash
# Install ImageMagick (Windows)
choco install imagemagick

# Resize logo
magick logo.png -resize 32x32 logo-32x32.png

# Create retina version
magick logo.png -resize 64x64 logo@2x.png

# Convert to WebP
magick hero.jpg -quality 85 hero.webp

# Optimize PNG
pngquant --quality=80-95 logo.png -o logo-optimized.png
```

---

## ?? Best Practices

### File Size Optimization

| Image Type | Max Size | Recommended Format |
|-----------|----------|-------------------|
| Navbar Logo | < 20KB | SVG (scalable, small) |
| Hero Banner | < 200KB | WebP + JPEG fallback |
| Card Thumbnail | < 50KB | WebP or optimized JPEG |
| Icons | < 10KB | SVG or PNG |
| Favicon | < 5KB | ICO + PNG |

### Performance Tips

? **Use SVG for Logos** - Scalable, crisp, tiny file size  
? **WebP + Fallback** - 25-35% smaller than JPEG/PNG  
? **Lazy Loading** - Use `loading="lazy"` for below-fold images  
? **Responsive Images** - Use `srcset` for different screen sizes  
? **CDN** - Host static images on CDN (Azure Blob Storage + CDN)  
? **Cache Headers** - Set long cache expiration for static assets

### Accessibility

```html
<!-- Good: Descriptive alt text -->
<img src="~/images/logo.svg" alt="Secure Boot Dashboard Logo">

<!-- Good: Decorative image -->
<img src="~/images/background.jpg" alt="" role="presentation">

<!-- Bad: Missing alt text -->
<img src="~/images/logo.svg">
```

---

## ?? Color Palette Recommendations

Per creare un logo coerente con la dashboard:

### Primary Colors

```css
/* Bootstrap Default Colors */
--bs-primary: #0d6efd;    /* Blue */
--bs-success: #198754;    /* Green */
--bs-danger: #dc3545;     /* Red */
--bs-warning: #ffc107;    /* Yellow */
--bs-info: #0dcaf0;       /* Cyan */

/* Dashboard Theme */
--bs-dark: #212529;       /* Navbar background */
--bs-light: #f8f9fa;      /* Light backgrounds */
```

### Logo Color Scheme

**Option 1: Blue/White (Trust & Security)**
- Primary: `#0d6efd` (Bootstrap Blue)
- Secondary: `#ffffff` (White)

**Option 2: Green/Blue (Safe & Secure)**
- Primary: `#198754` (Bootstrap Green)
- Secondary: `#0dcaf0` (Bootstrap Cyan)

**Option 3: Monochrome (Professional)**
- Primary: `#212529` (Dark Gray)
- Secondary: `#6c757d` (Medium Gray)

---

## ??? Example Logo SVG Code

```svg
<!-- Simple Shield Logo (32x32) -->
<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 32 32" width="32" height="32">
  <defs>
    <linearGradient id="shieldGradient" x1="0%" y1="0%" x2="0%" y2="100%">
      <stop offset="0%" style="stop-color:#0d6efd;stop-opacity:1" />
      <stop offset="100%" style="stop-color:#0dcaf0;stop-opacity:1" />
    </linearGradient>
  </defs>
  
  <!-- Shield Shape -->
  <path d="M16 2 L28 7 L28 15 Q28 25 16 30 Q4 25 4 15 L4 7 Z" 
        fill="url(#shieldGradient)" 
        stroke="#fff" 
        stroke-width="1"/>
  
  <!-- Checkmark -->
  <path d="M12 16 L15 19 L22 12" 
        fill="none" 
        stroke="#fff" 
        stroke-width="2" 
        stroke-linecap="round" 
        stroke-linejoin="round"/>
</svg>
```

---

## ? Checklist Pre-Launch

- [ ] Logo SVG created and optimized (< 10KB)
- [ ] Logo works on dark & light backgrounds
- [ ] Retina versions created (@2x, @3x)
- [ ] Hero banner responsive (desktop/tablet/mobile)
- [ ] Images compressed (WebP + fallback)
- [ ] Favicon generated (all sizes)
- [ ] Alt text added to all images
- [ ] Lazy loading enabled for below-fold images
- [ ] Images tested on different devices
- [ ] Performance audit passed (<200KB total page weight)

---

## ?? Quick Start Guide

### Add Logo Now (5 Minutes)

1. **Create SVG logo** using Figma or Canva
2. **Optimize** with https://jakearchibald.github.io/svgomg/
3. **Save** to `wwwroot/images/logo/logo.svg`
4. **Update** `_Layout.cshtml`:

```html
<a class="navbar-brand" asp-area="" asp-page="/Index">
    <img src="~/images/logo/logo.svg" 
         alt="Logo" 
         class="navbar-brand-logo">
    <span class="ms-2">Secure Boot Dashboard</span>
</a>
```

5. **Done!** Your logo is live.

---

## ?? Recommended Navbar Logo Specs

**TL;DR - Copy This:**

```
Format:      SVG (preferred) or PNG
Height:      32px (navbar)
Width:       Auto (max 150px)
File Size:   < 10KB
Colors:      Match dashboard theme (#0d6efd, #ffffff)
Background:  Transparent
```

**When you're ready to add a logo, just create the file and uncomment the line in `_Layout.cshtml`!**
