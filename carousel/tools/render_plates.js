// Renders the plate SVGs in ./plates to 1200x800 PNGs (run from tools/: NODE_PATH=$(npm root -g) node render_plates.js), then copy them to Carousel/Assets/Plates/.
const { chromium } = require('playwright'); const fs=require('fs');
(async()=>{const b=await chromium.launch();const p=await b.newPage({viewport:{width:1200,height:800}});
for(const f of fs.readdirSync('plates').filter(x=>x.endsWith('.svg'))){
 await p.setContent('<html><body style="margin:0">'+fs.readFileSync('plates/'+f,'utf8')+'</body></html>');
 await p.screenshot({path:'plates/'+f.replace('.svg','.png'),clip:{x:0,y:0,width:1200,height:800}});}
await b.close();})();
