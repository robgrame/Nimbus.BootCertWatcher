# Screenshots Guide

## ?? Screenshots da Creare per il README

Per completare la documentazione del README, è necessario creare i seguenti screenshot:

### 1. **Splash Screen** (`splash-screen.png`)
**Cattura:** Schermata di caricamento iniziale
- Mostra logo, titolo, spinner
- Sfondo gradient blu
- Dimensione consigliata: 1920x1080

### 2. **Dashboard Homepage** (`dashboard-home.png`)
**Cattura:** Homepage completa con grafici
- Card statistiche in alto
- 3 grafici (Compliance, Deployment, Trend)
- Pulsanti azioni rapide
- Dimensione consigliata: 1920x1080

### 3. **Device List Page** (`device-list.png`)
**Cattura:** Pagina lista dispositivi
- Mini-card statistiche
- Filtri (ricerca, stato, fleet)
- Tabella dispositivi completa
- Dimensione consigliata: 1920x1080

### 4. **Device Details Page** (`device-details.png`)
**Cattura:** Pagina dettaglio dispositivo
- Informazioni dispositivo
- Badge stato compliance
- Sezione certificati
- Dimensione consigliata: 1920x1080

### 5. **Charts Close-up** (`charts-closeup.png`)
**Cattura:** Focus sui grafici
- Zoom sui 3 grafici principali
- Tooltip visibile su hover
- Dimensione consigliata: 1600x900

### 6. **Mobile Responsive** (`mobile-responsive.png`)
**Cattura:** Vista mobile
- Screenshot da DevTools mobile view
- Mostra responsive design
- Dimensione consigliata: 375x812 (iPhone)

---

## ?? Posizionamento Screenshots

Creare directory nel repository:
```
Nimbus.BootCertWatcher/
??? docs/
    ??? screenshots/
        ??? splash-screen.png
        ??? dashboard-home.png
        ??? device-list.png
        ??? device-details.png
        ??? charts-closeup.png
        ??? mobile-responsive.png
```

---

## ?? Come Creare gli Screenshots

### Windows (Built-in)

**Cattura Schermo Intero:**
```
Windows + Print Screen
Salva automaticamente in: Pictures\Screenshots
```

**Cattura Area Selezionata:**
```
Windows + Shift + S
Apre Snipping Tool
Seleziona area e salva
```

### Browser DevTools (per Mobile)

```
1. F12 (apri DevTools)
2. Ctrl+Shift+M (toggle device toolbar)
3. Seleziona "iPhone 12 Pro" o "Responsive"
4. Cattura screenshot: Ctrl+Shift+P ? "Capture screenshot"
```

### PowerShell Script Automatico

```powershell
# scripts/Take-Screenshots.ps1
param(
    [string]$Url = "https://localhost:7001",
    [string]$OutputDir = ".\docs\screenshots"
)

# Requires Selenium WebDriver
# Install: Install-Package Selenium.WebDriver

Write-Host "Taking screenshots of Secure Boot Dashboard..." -ForegroundColor Cyan

# Create output directory
if (-not (Test-Path $OutputDir)) {
    New-Item -ItemType Directory -Path $OutputDir | Out-Null
}

# Note: Questo script richiede Selenium WebDriver
# Per semplicità, usare cattura manuale come descritto sopra
```

---

## ? Checklist Screenshots

### Prima di catturare:
- [ ] Dashboard popolata con dati di esempio
- [ ] Grafici visibili e renderizzati
- [ ] Nessun errore nella console
- [ ] Browser in dimensione standard (1920x1080)
- [ ] Zoom browser al 100%

### Per ogni screenshot:
- [ ] Nessun dato sensibile visibile
- [ ] UI completa e caricata
- [ ] Colori corretti (no dark mode se non richiesto)
- [ ] Alta qualità (PNG, no JPEG)

---

## ?? Editing Screenshots (Opzionale)

### Strumenti Consigliati:
- **Windows:** Paint 3D, Snip & Sketch
- **Online:** Photopea.com (simile a Photoshop)
- **Professional:** GIMP (gratis, open source)

### Modifiche Comuni:
1. **Crop**: rimuovere bordi inutili
2. **Blur**: oscurare dati sensibili
3. **Arrow/Box**: evidenziare funzionalità
4. **Text**: aggiungere note esplicative

---

## ?? Dimensioni File

**Target:**
- Max 500 KB per screenshot
- PNG format (migliore per UI)
- 1920x1080 o inferiore

**Compressione:**
- Online: tinypng.com
- Desktop: PNGGauntlet (Windows)
- CLI: pngquant

```powershell
# Esempio compressione con ImageMagick
magick convert input.png -quality 85 -resize 1920x1080 output.png
```

---

## ?? Upload a GitHub

### Via Git:
```bash
# Aggiungi screenshots
git add docs/screenshots/*.png

# Commit
git commit -m "docs: add dashboard screenshots"

# Push
git push origin main
```

### Via GitHub Web:
1. Vai a repository su GitHub.com
2. Navigate to `docs/screenshots/`
3. Click "Add file" ? "Upload files"
4. Drag & drop screenshots
5. Commit changes

---

## ??? Riferimento nel README

Una volta caricati gli screenshots, aggiungerli al README con:

```markdown
## ?? Screenshots

### Dashboard Homepage
![Dashboard Homepage](docs/screenshots/dashboard-home.png)

### Device List
![Device List](docs/screenshots/device-list.png)

### Device Details
![Device Details](docs/screenshots/device-details.png)

### Charts Analytics
![Charts](docs/screenshots/charts-closeup.png)

### Mobile Responsive
<img src="docs/screenshots/mobile-responsive.png" alt="Mobile View" width="375">
```

---

## ? Screenshot Perfetti

**Suggerimenti per screenshot professionali:**

1. **Dati Realistici:**
   - Usare nomi dispositivi realistici (PC-001, LAPTOP-HR-15)
   - Variare stati (Deployed, Pending, Error)
   - Date recenti e credibili

2. **UI Pulita:**
   - Nessun popup/modal aperto (salvo se feature da mostrare)
   - Navbar visibile
   - Footer visibile (opzionale)

3. **Evidenziare Features:**
   - Hover su card per mostrare effetto
   - Tooltip grafico visibile
   - Badge e icone ben visibili

4. **Consistenza:**
   - Stessa risoluzione per tutti
   - Stesso zoom level
   - Stesso tema (light/dark)

---

## ?? Priority Screenshots

**Must-have (essenziali per README):**
1. ? Dashboard Homepage
2. ? Device List
3. ? Charts Close-up

**Nice-to-have (opzionali):**
4. Device Details
5. Mobile Responsive
6. Splash Screen

---

Questo file guida la creazione degli screenshot per documentare visualmente il Secure Boot Dashboard nel README.
