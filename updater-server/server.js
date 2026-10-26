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
            version: "2.0.11",
            downloadUrl: `${req.protocol}://${req.get('host')}/Universe_Setup_v2.0.11.exe`,
            sha256: "64458733b2f4f9194921f4d2627e451a5cb966802e78ffac1b8042ebd520cc72",
            releaseNotes: "Universe v2.0.11: ventana de Ajustes renovada y traducida al espanol, iconos corregidos, deslizadores de color nuevos y cambio de idioma al instante."
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
