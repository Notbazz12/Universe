const express = require('express');
const cors = require('cors');
const path = require('path');
const fs = require('fs');
const https = require('https');
const http = require('http');

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
            version: "2.0.9",
            downloadUrl: `${req.protocol}://${req.get('host')}/Universe_Setup_v2.0.9.exe`,
            sha256: "59bda70e6528863197017a898f2833ace8ea8354020058c1e8e26124f306a1da",
            releaseNotes: "Universe v2.0.9: Specialized Images & Tasks fences with auto-import and file filtering, plus compact icon sizes (16px, 20px, 24px) with smooth Ctrl + Mouse Wheel zoom!"
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
    
    // Auto Keep-Alive Self Ping (cada 12 minutos)
    const serverUrl = process.env.RENDER_EXTERNAL_URL || 'https://universe-update-server.onrender.com';
    setInterval(() => {
        const client = serverUrl.startsWith('https') ? https : http;
        client.get(`${serverUrl}/health`, (res) => {
            console.log(`[Keep-Alive] Ping sent to ${serverUrl}/health -> Status ${res.statusCode}`);
        }).on('error', (err) => {
            console.log(`[Keep-Alive] Ping error: ${err.message}`);
        });
    }, 12 * 60 * 1000); // 12 minutos
});
