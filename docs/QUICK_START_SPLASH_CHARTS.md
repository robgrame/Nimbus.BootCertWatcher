# ?? Quick Start Guide - Splash Screen & Charts

## ? Novità Implementate

Sono state aggiunte due nuove funzionalità al Secure Boot Dashboard:

### 1. ?? Splash Screen
- **Schermata di caricamento** elegante con logo animato
- **Fade-out automatico** dopo il caricamento
- **Design responsive** ottimizzato per tutti i dispositivi

### 2. ?? Grafici Interattivi
- **Compliance Status**: percentuale dispositivi compliant
- **Deployment States**: distribuzione stati (Deployed, Pending, Error)
- **Compliance Trend**: crescita compliance negli ultimi 7 giorni

---

## ?? Avvio Rapido

### 1. Avviare il Dashboard

```powershell
# Terminal 1 - Avvia API
cd SecureBootDashboard.Api
dotnet run

# Terminal 2 - Avvia Web
cd SecureBootDashboard.Web
dotnet run
```

### 2. Aprire il Browser

```
https://localhost:7001/
```

### 3. Esperienza Utente

1. **Splash Screen** appare immediatamente
   - Logo animato
   - Spinner rotante
   - Testo "Caricamento in corso..."

2. **Dashboard si carica** (dopo 1-5 secondi)
   - Splash scompare con fade-out
   - Card statistiche visibili
   - **Grafici renderizzati sotto le card**

3. **Interagisci con i grafici**
   - Passa il mouse sui grafici per vedere dettagli
   - Visualizza percentuali e valori
   - Grafici si adattano al resize della finestra

---

## ?? Grafici Disponibili

### 1. Compliance Status (Donut Chart)
**Posizione:** Prima colonna  
**Mostra:** Percentuale dispositivi compliant vs non-compliant  
**Colori:**
- ?? Verde = Compliant (Deployed)
- ?? Rosso = Non-Compliant (Pending/Error/Unknown)

**Metrica Centrale:** X.X% Compliant

### 2. Deployment States (Pie Chart)
**Posizione:** Seconda colonna  
**Mostra:** Distribuzione degli stati di deployment  
**Colori:**
- ?? Verde = Deployed
- ?? Giallo = Pending
- ?? Rosso = Error
- ? Grigio = Unknown

### 3. Compliance Trend (Line Chart)
**Posizione:** Terza colonna  
**Mostra:** Crescita compliance negli ultimi 7 giorni  
**Colori:**
- ?? Linea blu con area riempita

**Asse X:** Date (MMM GG)  
**Asse Y:** Numero dispositivi compliant

---

## ?? Visualizzazione Responsive

### Desktop (>992px)
```
[Card1] [Card2] [Card3] [Card4] [Card5] [Card6]

[Chart1]      [Chart2]      [Chart3]
  33.3%         33.3%         33.3%

[Devices Table - Full Width]
```

### Tablet (768-991px)
```
[Card1] [Card2] [Card3]
[Card4] [Card5] [Card6]

[Chart1]  [Chart2]
  50%       50%

[Chart3 - Full Width]
   100%

[Devices Table]
```

### Mobile (<768px)
```
[Title & API Status]

[Card1] [Card2] [Card3]
[Card4] [Card5] [Card6]

[Chart1 - Full Width]
[Chart2 - Full Width]
[Chart3 - Full Width]

[Devices Table]
```

**Nota:** Il banner hero è nascosto su mobile per risparmiare spazio.

---

## ?? Interpretare i Grafici

### Compliance Status

**Alto Compliance (>80%):**
```
?? Compliant:     85%
?? Non-Compliant: 15%

Significato: La maggior parte dei dispositivi ha il certificato Secure Boot aggiornato
Azione:      Concentrarsi sui dispositivi rimanenti
```

**Basso Compliance (<50%):**
```
?? Compliant:     40%
?? Non-Compliant: 60%

Significato: Molti dispositivi necessitano aggiornamento
Azione:      Pianificare deployment urgente
```

### Deployment States

**Situazione Ideale:**
```
?? Deployed:  80%
?? Pending:   15%
?? Error:      3%
? Unknown:     2%

Significato: Deployment procede bene, pochi errori
Azione:      Monitorare pending, investigare errori
```

**Situazione Problematica:**
```
?? Deployed:  30%
?? Pending:   40%
?? Error:     25%
? Unknown:     5%

Significato: Molti dispositivi in errore
Azione:      Investigare cause errori, verificare configurazione client
```

### Compliance Trend

**Crescita Positiva:**
```
      ????
    ??
  ??
??

Significato: Compliance sta aumentando
Azione:      Continuare con deployment plan
```

**Crescita Piatta:**
```
????????????

Significato: Nessun progresso
Azione:      Verificare che i client stiano inviando dati
             Controllare eventuali blocchi deployment
```

**Crescita Negativa:**
```
??
  ??
    ????

Significato: Dispositivi diventano non-compliant
Azione:      ALERT! Investigare causa (scadenza certificati? rollback?)
```

---

## ?? Personalizzazioni

### Modificare Colori Splash Screen

**File:** `SecureBootDashboard.Web/wwwroot/css/splash.css`

```css
.splash-screen {
    /* Cambia questi colori */
    background: linear-gradient(135deg, #1e3c72 0%, #2a5298 100%);
    
    /* Esempi alternativi: */
    /* Dark:   #212529 ? #343a40 */
    /* Green:  #155724 ? #28a745 */
    /* Purple: #6f42c1 ? #8e44ad */
}
```

### Modificare Tempo Splash

**File:** `SecureBootDashboard.Web/wwwroot/js/splash.js`

```javascript
// Linea 16
const minDisplayTime = 1000; // millisecondi

// Per tempi diversi:
const minDisplayTime = 2000; // 2 secondi
const minDisplayTime = 500;  // 0.5 secondi (veloce)
```

### Modificare Periodo Trend

**File:** `SecureBootDashboard.Web/Pages/Index.cshtml.cs`

```csharp
// Metodo CalculateComplianceTrend()
// Linea ~60

// Da 7 giorni a 30 giorni:
for (int i = 29; i >= 0; i--)
{
    var date = today.AddDays(-i);
    // ...
}
```

**Nota:** Richiede dati storici reali per essere accurato in produzione.

### Disabilitare Splash Screen

**Opzione 1 - Rimuovere da Layout**

File: `Pages/Shared/_Layout.cshtml`

Commenta o rimuovi:
```html
@* Splash Screen *@
<!--
<div id="splash-screen" class="splash-screen">
    ...
</div>
-->
```

**Opzione 2 - CSS Override**

File: `wwwroot/css/site.css`

```css
#splash-screen {
    display: none !important;
}
```

---

## ?? Test e Verifica

### 1. Test Splash Screen

```powershell
# Apri DevTools (F12)
# Network tab ? Throttling ? "Slow 3G"
# Ricarica pagina (Ctrl+Shift+R)
# Splash dovrebbe essere visibile per almeno 1 secondo
```

**Verifica:**
- ? Splash appare immediatamente
- ? Logo è visibile e animato
- ? Spinner ruota
- ? Scompare con fade-out
- ? Dashboard appare dopo

### 2. Test Grafici

**Console Browser (F12):**
```javascript
// Verifica Chart.js caricato
console.log(typeof Chart); // Dovrebbe essere "function"

// Verifica dati
console.log(document.querySelectorAll('canvas').length); // Dovrebbe essere 3
```

**Interazioni:**
- ? Passa mouse su grafico ? tooltip appare
- ? Resize finestra ? grafici si adattano
- ? Mobile view ? grafici impilati verticalmente

### 3. Test con Dati Simulati

**File:** `Pages/Index.cshtml.cs`

Decommentare sezione mock devices:
```csharp
// In OnGetAsync(), dopo il check ApiHealthy
if (Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development")
{
    // Generate 50 mock devices
    var mockDevices = new List<DeviceSummary>();
    for (int i = 0; i < 50; i++)
    {
        // ... codice mock ...
    }
    Devices = mockDevices;
}
```

Riavviare e vedere dashboard popolata con dati test.

---

## ?? Troubleshooting

### Splash Non Scompare

**Problema:** Splash screen rimane visibile indefinitamente

**Diagnosi:**
```javascript
// Console browser
document.getElementById('splash-screen').classList.contains('fade-out');
// Se "false", il fade-out non è stato attivato
```

**Soluzioni:**
1. Ricarica pagina hard (Ctrl+Shift+R)
2. Verifica errori console (F12)
3. Rimuovi manualmente:
   ```javascript
   document.getElementById('splash-screen').remove();
   ```

### Grafici Non Visualizzati

**Problema:** Canvas vuoto, nessun grafico

**Diagnosi:**
```javascript
// Console browser
console.log(typeof Chart); // Verifica Chart.js caricato
console.log(@Model.TotalDevices); // Verifica dati presenti
```

**Soluzioni:**
1. Verifica Chart.js CDN raggiungibile
2. Controlla errori console
3. Verifica che ci siano dispositivi (`Model.Devices.Any()`)

### Grafici Distorti

**Problema:** Grafici troppo grandi/piccoli su mobile

**Soluzione:**

File: `Pages/Index.cshtml` ? `@section Styles`

```css
/* Aggiungi/modifica: */
.card-body canvas {
    max-height: 250px; /* Regola altezza massima */
}
```

### Percentuali Errate

**Problema:** Percentuali non sommano a 100%

**Diagnosi:**
```javascript
// Console browser (su pagina Index)
const total = @Model.TotalDevices;
const compliant = @Model.CompliantDevices;
const nonCompliant = @Model.NonCompliantDevices;
console.log(compliant + nonCompliant === total); // Dovrebbe essere true
```

**Causa:** Possibile bug nel calcolo `NonCompliantDevices`

**Soluzione:** Verifica logica in `Index.cshtml.cs`

---

## ?? Metriche Prestazioni

### Load Times Attesi

```
Splash Screen:
  Render:      < 50ms
  Visible:     1000-5000ms
  Fade-out:    500ms

Chart.js:
  Download:    ~500ms (3G)
  Parse:       ~100ms
  Render:      ~300ms (3 grafici)

Total Page Load:
  First Paint: ~200ms
  Interactive: ~2000ms
```

### Lighthouse Score (Obiettivi)

```
Performance:     90+
Accessibility:   95+
Best Practices:  90+
SEO:             90+
```

---

## ?? Documentazione Completa

### File Documentazione

```
docs/
??? DASHBOARD_CHARTS_SPLASH_IMPLEMENTATION.md  ? Dettagli tecnici
??? DASHBOARD_VISUAL_SUMMARY.md                ? Riepilogo visuale
??? QUICK_START_SPLASH_CHARTS.md (questo)      ? Guida rapida
```

### Link Utili

- **Chart.js Docs:** https://www.chartjs.org/docs/latest/
- **Bootstrap Grid:** https://getbootstrap.com/docs/5.3/layout/grid/
- **CSS Animations:** https://developer.mozilla.org/en-US/docs/Web/CSS/CSS_Animations

---

## ?? Prossimi Passi

### Immediate Actions

1. ? **Test Dashboard**
   - Avvia Web app
   - Verifica splash screen
   - Verifica grafici

2. ? **Popola Dati**
   - Avvia client sui dispositivi
   - Attendi invio primi report
   - Verifica grafici popolati

3. ? **Monitor Compliance**
   - Controlla percentuale compliance
   - Identifica dispositivi non-compliant
   - Pianifica deployment

### Future Enhancements

- [ ] **Dati Storici Reali**
  - Implementare snapshot giornalieri
  - Query database per trend storici
  - Estendere periodo trend (30/60 giorni)

- [ ] **Grafici Aggiuntivi**
  - Manufacturer distribution
  - Fleet comparison
  - Certificate expiration timeline

- [ ] **Export & Share**
  - Download grafici come PNG
  - PDF report generation
  - Email alerts

- [ ] **Dashboard Personalizzabile**
  - User preferences per grafici
  - Layout drag-and-drop
  - Dark mode support

---

## ? Checklist Completamento

### Implementazione
- [x] Splash screen creato
- [x] CSS splash implementato
- [x] JavaScript splash implementato
- [x] Grafici aggiunti alla dashboard
- [x] Chart.js integrato
- [x] Responsive design applicato
- [x] Build successful

### Testing
- [ ] Splash screen testato
- [ ] Grafici testati (interattività)
- [ ] Responsive testato (mobile/tablet/desktop)
- [ ] Performance testata (Lighthouse)
- [ ] Browser compatibility testata

### Deployment
- [ ] Produzione: verificare CDN accessibile
- [ ] Produzione: verificare logo presente
- [ ] Produzione: monitorare errori console
- [ ] Produzione: raccogliere feedback utenti

---

## ?? Supporto

### In caso di problemi:

1. **Controllare logs:**
   ```powershell
   # Web app logs
   dotnet run --project SecureBootDashboard.Web
   
   # Controllare output console
   ```

2. **Browser DevTools:**
   - F12 ? Console (errori JavaScript)
   - F12 ? Network (file non caricati)
   - F12 ? Elements (inspect HTML/CSS)

3. **GitHub Issues:**
   - Aprire issue: https://github.com/robgrame/Nimbus.BootCertWatcher/issues
   - Includere screenshot e logs

4. **Documentazione:**
   - Consultare `docs/` per dettagli tecnici
   - Leggere `README.md` per overview

---

## ?? Conclusione

**Le nuove funzionalità sono pronte!** ??

Hai ora:
- ? Splash screen professionale
- ? 3 grafici interattivi
- ? Dashboard moderna e user-friendly
- ? Visualizzazione compliance chiara
- ? Trend temporale monitoring

**Buon monitoraggio!** ???
