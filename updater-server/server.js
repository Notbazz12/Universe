const express = require('express');
const cors = require('cors');
const path = require('path');
const fs = require('fs');

const app = express();
app.use(cors());

// Servir la carpeta pública estática (Landing Page e Instaladores)
app.use(express.static(path.join(__dirname, 'public')));

// Health Check Endpoint (para Keep-Alive 24/7 y Monitoreo)
app.get('/health', (req, res) => {
    res.json({
        status: 'online',
        server: 'Universe Cloud Server',
        uptimeSeconds: Math.floor(process.uptime()),
        timestamp: new Date().toISOString()
    });
});

// Endpoint de la API que consulta Universe para comprobar actualizaciones automáticas
app.get('/version.json', (req, res) => {
    const versionFilePath = path.join(__dirname, 'public', 'version.json');
    
    if (fs.existsSync(versionFilePath)) {
        const rawData = fs.readFileSync(versionFilePath, 'utf8');
        const data = JSON.parse(rawData);
        
        if (!data.downloadUrl) {
            const protocol = req.headers['x-forwarded-proto'] || req.protocol;
            const host = req.get('host');
            data.downloadUrl = `${protocol}://${host}/Universe_Setup_v${data.version}.exe`;
        }
        
        res.json(data);
    } else {
        res.json({
            version: "2.0.13",
            downloadUrl: `${req.protocol}://${req.get('host')}/Universe_Setup_v2.0.13.exe`,
            sha256: "0d76ccb4637486b3c209e3a71417d3b41705290b254d662c013fc8e99684f243",
            releaseNotes: "Universe v2.0.13: la mascota pasea sola, salta de verdad, se duerme si no usas el PC y se alegra al soltar archivos. Nuevas mascotas: Sakura, Esmeralda, Solar, Nebulosa, Zafiro y Sombra, mas tamanos."
        });
    }
});

// Fallback para servir index.html
app.get('*', (req, res) => {
    res.sendFile(path.join(__dirname, 'public', 'index.html'));
});

const PORT = process.env.PORT || 3000;
app.listen(PORT, () => {
    console.log(`✦ Universe Website & Auto-Updater Server listening on port ${PORT}`);
    // Hosted on Railway: services stay awake, so no keep-alive self ping is needed
});
