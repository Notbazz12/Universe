const express = require('express');
const cors = require('cors');
const path = require('path');
const fs = require('fs');

const app = express();
app.use(cors());

// Servir la carpeta pública estática (Landing Page e Instaladores)
app.use(express.static(path.join(__dirname, 'public')));

// Endpoint de la API que consulta Universe para comprobar actualizaciones automáticas
app.get('/version.json', (req, res) => {
    const versionFilePath = path.join(__dirname, 'public', 'version.json');
    
    if (fs.existsSync(versionFilePath)) {
        const rawData = fs.readFileSync(versionFilePath, 'utf8');
        const data = JSON.parse(rawData);
        
        const protocol = req.headers['x-forwarded-proto'] || req.protocol;
        const host = req.get('host');
        data.downloadUrl = `${protocol}://${host}/Universe_Setup_v2.0.0.exe`;
        
        res.json(data);
    } else {
        res.json({
            version: "2.0.0",
            downloadUrl: `${req.protocol}://${req.get('host')}/Universe_Setup_v2.0.0.exe`,
            sha256: "7dcc30f64eeaeeb91b554a8fe53c3545b41491ec777bb89e625e25b6d20cd20a",
            releaseNotes: "Universe v2.0.0: Cyber-Glass & Iridescent Bubble Edition"
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
});
