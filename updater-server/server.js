const express = require('express');
const cors = require('cors');
const path = require('path');
const fs = require('fs');

const app = express();
app.use(cors());

// Servir la carpeta pública de descargas e instaladores
app.use(express.static(path.join(__dirname, 'public')));

// Endpoint que consulta la aplicación Universe para verificar actualizaciones
app.get('/version.json', (req, res) => {
    const versionFilePath = path.join(__dirname, 'public', 'version.json');
    
    if (fs.existsSync(versionFilePath)) {
        const rawData = fs.readFileSync(versionFilePath, 'utf8');
        const data = JSON.parse(rawData);
        
        // Ajustar dinámicamente el host al dominio de Render
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

// Endpoint de salud (Health Check)
app.get('/', (req, res) => {
    res.send(`
        <html>
            <head><title>Universe Update Server</title></head>
            <body style="background:#0E1017; color:#fff; font-family:sans-serif; text-align:center; padding:50px;">
                <h1 style="color:#00F5D4;">✦ Universe Update Server is Running!</h1>
                <p>Latest Version Manifest: <a href="/version.json" style="color:#8B5CF6;">/version.json</a></p>
            </body>
        </html>
    `);
});

const PORT = process.env.PORT || 3000;
app.listen(PORT, () => {
    console.log(`Universe Updater Server listening on port ${PORT}`);
});
