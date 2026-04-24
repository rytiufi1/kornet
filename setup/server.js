const express = require('express');
const path = require('path');
const fs = require('fs');

const app = express();
const port = 6769;

app.get('/', (req, res) => {
    res.send('what are u doing here');
});

app.get('*', (req, res) => {
    const relativePath = req.path.slice(1);
    const filesfolder = path.join(__dirname, 'files', relativePath);

    const filesRoot = path.join(__dirname, 'files');
    if (!filesfolder.startsWith(filesRoot)) {
        return res.status(403).send('nope');
    }

    if (fs.existsSync(filesfolder)) {
        res.download(filesfolder);
        console.log(`giving the guy ${filesfolder}`);
    } else {
        console.log(`couldnt find ${filesfolder}`);
        res.status(404).send('not found');
    }
});

app.listen(port, 'localhost', () => {
    console.log(`hell yea it started at http://localhost:${port}`);
});