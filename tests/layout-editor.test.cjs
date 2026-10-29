// Exercise the shipped, dependency-free LED Studio script in its offline mode.
const fs=require('fs'),vm=require('vm'),assert=require('assert');
const html=fs.readFileSync('app/res/Layout.html','utf8');
const script=html.match(/<script>([\s\S]*?)<\/script>/)[1],elements=new Map();
for(const tag of html.matchAll(/<([a-z0-9]+)\b[^>]*\bid="([^"]+)"[^>]*>/gi)){
 const value=tag[0].match(/value="([^"]*)"/);
 elements.set(tag[2],{value:value?value[1]:'',textContent:'',innerHTML:'',hidden:false,disabled:false,className:'',dataset:{},
  classList:{add(){},remove(){},toggle(){},contains(){return false;}},setAttribute(){},addEventListener(){},click(){},focus(){},select(){}});
}
const context={document:{getElementById:id=>elements.get(id),documentElement:{},activeElement:null},window:{isSecureContext:false},
 location:{protocol:'file:'},navigator:{},console,getComputedStyle:()=>({getPropertyValue:()=>''}),
 addEventListener(){},setTimeout(){},clearTimeout(){},setInterval(){},clearInterval(){},confirm:()=>true,fetch:()=>Promise.reject(new Error('offline'))};
vm.runInNewContext(script,context,{timeout:1000});
const editor=context.window.LayoutEditor,clone=o=>JSON.parse(JSON.stringify(o));
const canon=o=>JSON.stringify(o,(k,v)=>v&&typeof v==='object'&&!Array.isArray(v)?Object.keys(v).sort().reduce((a,x)=>(a[x]=v[x],a),{}):v);
const checkTvDoc=(doc,why)=>{assert.equal(doc.schema,1,why);assert(doc.leds.length>=4&&doc.leds.length<=490,why);
 doc.leds.forEach((l,i)=>{for(const k of['hmin','hmax','vmin','vmax'])assert(l[k]>=0&&l[k]<=1,why+': LED '+i+' '+k);
  assert(l.hmin<l.hmax&&l.vmin<l.vmax,why+': LED '+i+' empty zone');assert(l.x>=-2&&l.x<=3&&l.y>=-2&&l.y<=3,why+': LED '+i+' position');});};

// offline start: a usable TV frame, exported in the format the TV accepts, with the drawing alongside
assert.equal(editor.total(),96,'Offline editor must open on a usable TV frame');
const doc=editor.exportDoc();checkTvDoc(doc,'Exported frame');assert.equal(doc.studio.v,1,'Export must carry the drawing');

// a drawing with hand-set rows, edges and a connection comes back exactly
const drawn={schema:1,name:'TV and stand',studio:{v:1,reversed:true,
 verts:[{x:.5,y:1.04},{x:1.04,y:1.04},{x:1.04,y:-.04},{x:-.04,y:-.04},{x:-.04,y:1.04},{x:.4,y:1.04},{x:.6,y:1.125},{x:1.04,y:1.125},{x:1.04,y:1.25},{x:.6,y:1.25}],
 segs:[{type:'led',leds:21,zEdge:'bottom',zFrom:.5,zTo:1,zDepth:.1,zLayer:0},{type:'led',leds:19,zEdge:'right',zFrom:0,zTo:1,zDepth:.1,zLayer:0},
  {type:'led',leds:21,zEdge:'top',zFrom:0,zTo:1,zDepth:.1,zLayer:0},{type:'led',leds:18,zEdge:'left',zFrom:0,zTo:1,zDepth:.1,zLayer:0},
  {type:'led',leds:21,zEdge:'bottom',zFrom:0,zTo:.5,zDepth:.1,zLayer:0},{type:'link',leds:0},
  {type:'led',leds:20,zEdge:'bottom',zFrom:.75,zTo:1,zDepth:.08,zLayer:1},{type:'led',leds:19,zEdge:'right',zFrom:.875,zTo:.995,zDepth:.05,zLayer:1},
  {type:'led',leds:20,zEdge:'bottom',zFrom:.875,zTo:1,zDepth:.05,zLayer:2}]}};
const want=(({v,...rest})=>rest)(drawn.studio);
assert.equal(editor.adopt(clone(drawn)),'exact');assert.equal(canon(editor.state()),canon(want),'Drawing must round-trip exactly');
const saved=editor.exportDoc();checkTvDoc(saved,'Exported stand layout');assert.equal(saved.leds.length,159);
assert.equal(editor.adopt(clone(saved)),'exact');assert.equal(canon(editor.state()),canon(want),'Export then import must not change anything');

// a drawing that no longer matches its LED list is not trusted: the LEDs are rebuilt instead
const stale=clone(saved);stale.leds.pop();assert.equal(editor.adopt(stale),'rebuilt','A stale drawing must be ignored');

// rebuilding a plain LED list: one segment per edge, corner LEDs absorbed, the stand on its own row
const forward=clone(drawn);forward.studio.reversed=false;editor.adopt(forward);
const flat=editor.exportDoc();delete flat.studio;assert.equal(editor.adopt(flat),'rebuilt');
const segs=editor.state().segs,leds=segs.filter(s=>s.type==='led');
assert.equal(editor.total(),159,'Rebuild keeps every LED');assert(leds.every(s=>s.leds>1),'No 1-LED corner strays');
assert(segs.some(s=>s.type==='link'),'A gap down to the stand becomes a connection');
assert.deepEqual(leds.slice(0,5).map(s=>s.zEdge),['bottom','right','top','left','bottom'],'TV frame edges come back in order');
assert(leds.slice(5).every(s=>s.zLayer>=1),'Stand segments sit on an outer row');

// a standard frame from the TV has no mounted positions: LEDs go just outside the edge they sample
const plain=saved.leds.slice(0,100).map(l=>({hmin:l.hmin,hmax:l.hmax,vmin:l.vmin,vmax:l.vmax}));
assert.equal(editor.adopt({schema:1,leds:plain}),'rebuilt');
checkTvDoc(editor.exportDoc(),'Rebuilt TV frame');
assert(editor.exportDoc().leds.every(l=>l.x<0||l.x>1||l.y<0||l.y>1),'Rebuilt frame LEDs must sit outside the picture');
// the TV's own standard frame shares its corner zones between two edges (as ZoneLayout builds it):
// it must come back as four continuous segments, not split by invented connections
const tvFrame=[];const zone=(hmin,hmax,vmin,vmax)=>tvFrame.push({hmin,hmax,vmin,vmax});
for(let i=0;i<32;i++)zone(i/32,(i+1)/32,0,.08);for(let i=0;i<18;i++)zone(.92,1,i/18,(i+1)/18);
for(let i=31;i>=0;i--)zone(i/32,(i+1)/32,.92,1);for(let i=17;i>=0;i--)zone(0,.08,i/18,(i+1)/18);
editor.adopt({schema:1,name:'TV frame',leds:tvFrame});const tvSegs=editor.state().segs;
assert(tvSegs.every(s=>s.type==='led'),'A standard frame has no connections');
assert.deepEqual(tvSegs.map(s=>s.zEdge),['top','right','bottom','left'],'A standard frame is one segment per side');
assert.equal(editor.total(),100);

// bad files are refused with a reason and leave the drawing alone
const before=canon(editor.state());
assert.throws(()=>editor.adopt({schema:2,leds:plain}));
assert.throws(()=>editor.adopt({leds:Array(491).fill(plain[0])}),/4 to 490/);
assert.throws(()=>editor.adopt({leds:plain.map((l,i)=>i?l:{...l,hmax:1.5})}),/coordinates/);
editor.importText('{nope');assert(elements.get('message').textContent.includes('Not valid JSON'),'Broken JSON must be explained');
assert.equal(canon(editor.state()),before,'A refused file must not change the drawing');

// buttons: TV frame, clear, undo, LED limits
elements.get('frame').onclick();assert.equal(editor.total(),96);
elements.get('clear').onclick();assert.equal(editor.total(),0);assert(/at least 4/.test(editor.problem()));
elements.get('undo').onclick();assert.equal(editor.total(),96,'Undo restores the frame');
editor.adopt({schema:1,studio:{v:1,reversed:false,verts:[{x:0,y:-.04},{x:1,y:-.04},{x:1,y:1.04}],segs:[{type:'led',leds:300},{type:'led',leds:191}]}});
assert(/at most 490/.test(editor.problem()),'An over-long strip must be flagged');

// live preview on the strip: one colour, everything else off
elements.get('frame').onclick();const colours=()=>editor.previewHex().match(/.{6}/g);
editor.select(null);let c=colours();assert.equal(c.length,96);assert(c.every(x=>x==='ffc800'),'Nothing selected lights every LED, in one colour');
editor.select(1);c=colours();assert(c.every((x,i)=>x===(i>=30&&i<48?'ffc800':'000000')),'Only the selected segment lights up, the rest is off');
// connections hold no LEDs: a selected cable lights the two LEDs it sits between; converting one keeps the count honest
editor.adopt(clone(forward));editor.select(5);assert.equal(editor.state().segs[5].type,'link');
c=colours();assert.deepEqual(c.map((x,i)=>x==='ffc800'?i:-1).filter(i=>i>=0),[99,100],'A selected cable lights the two LEDs it sits between');
editor.select(1);elements.get('typeLink').onclick();assert.equal(editor.total(),159-19,'A cable holds no LEDs');
assert(/7 segments · 2 connections/.test(elements.get('segcount').textContent),'The badge counts connections apart from segments');
elements.get('typeLed').onclick();assert.equal(editor.total(),159,'Turning a cable back into LEDs restores its count');

// dragging a block adds up small moves instead of losing them to the grid snap
editor.adopt(clone(drawn));editor.dragMove([{j:0,f:.5,t:1,L:0}].map(o=>({...o,t:.7})),60,0);
assert(editor.state().segs[0].zFrom>.5,'Small drag steps must add up');
console.log('PASS: LED Studio runs offline; TV-valid export, exact drawing round-trip, stale-drawing guard, rebuild of plain and stand layouts, refused files, undo, limits, live preview, connections, block drag');
