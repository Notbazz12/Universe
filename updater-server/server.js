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
            version: "2.0.12",
            downloadUrl: `${req.protocol}://${req.get('host')}/Universe_Setup_v2.0.12.exe`,
            sha256: "f0d0c0d0684da17c6a183defa710296e979778e9e0c90640ed86302b92fe76ae",
            releaseNotes: "Universe v2.0.12: nueva notificacion de actualizacion con logo, menu de las fences renovado con iconos y traducido, Ordenar por (nombre, tipo, fecha) y titulos con emoji visibles."
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
