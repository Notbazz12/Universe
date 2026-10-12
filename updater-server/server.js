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
        
        const protocol = req.headers['x-forwarded-proto'] || req.protocol;
        const host = req.get('host');
        data.downloadUrl = `${protocol}://${host}/Universe_Setup_v2.0.4.exe`;
        
        res.json(data);
    } else {
        res.json({
            version: "2.0.4",
            downloadUrl: `${req.protocol}://${req.get('host')}/Universe_Setup_v2.0.4.exe`,
            sha256: "239f6144318045a1e2f20cdc56e1573d35e67e83942cef10f0d1f6ce880a7daa",
            releaseNotes: "Universe v2.0.4: Smart Program & Application detection, multi-program folder unpacking, and 1-click desktop organization."
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
