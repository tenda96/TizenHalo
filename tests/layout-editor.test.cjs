// Exercise the shipped, dependency-free editor script in its offline mode.
const fs=require('fs'),vm=require('vm'),assert=require('assert');
const html=fs.readFileSync('app/res/Layout.html','utf8');
const script=html.match(/<script>([\s\S]*?)<\/script>/)[1],elements=new Map();
for(const tag of html.matchAll(/<(?:input|select|button|svg|div|span|p|a|details)[^>]*id="([^"]+)"[^>]*>/g)){
 const value=tag[0].match(/value="([^"]*)"/);elements.set(tag[1],{value:value?value[1]:'',checked:false,disabled:false,classList:{add(){},remove(){}},addEventListener(){},setAttribute(){},click(){}});
}
const context={document:{getElementById:id=>elements.get(id)},window:{},location:{protocol:'file:'},console,addEventListener(){}};
vm.runInNewContext(script,context,{timeout:1000});const editor=context.window.LayoutEditor;
assert.equal(editor.getDocument().leds.length,96,'Offline editor must be usable without a TV');
const forward=editor.frame([12,8,12,8],2,false,.08),reverse=editor.frame([12,8,12,8],2,true,.08);
assert.equal(forward.leds.length,40);assert.deepEqual(JSON.parse(JSON.stringify(forward.leds[0])),JSON.parse(JSON.stringify(reverse.leds[0])),'Reverse must keep LED zero at the chosen corner');
assert(forward.leds[0].vmin>.8&&forward.leds[0].hmin>.9,'Bottom-right start');
const furniture=editor.segment({x:0,y:1.3},{x:1,y:1.3},20,'bottom',.1);
assert(furniture.every(l=>l.y===1.3&&l.vmin===.9&&l.vmax===1),'Furniture below TV must sample bottom picture');
assert.equal(editor.valid({schema:1,leds:forward.leds.concat(furniture)}).leds.length,60);
assert.throws(()=>editor.valid({schema:2,leds:forward.leds}));
assert.throws(()=>editor.valid([{hmin:0,hmax:0,vmin:0,vmax:1}]));
assert.throws(()=>editor.valid({leds:Array(491).fill(forward.leds[0])}));
elements.get('generate').onclick();assert.equal(editor.getDocument().leds.length,96);
elements.get('duplicate').onclick();assert.equal(editor.getDocument().leds.length,97);
elements.get('undo').onclick();assert.equal(editor.getDocument().leds.length,96);
elements.get('delete').onclick();assert.equal(editor.getDocument().leds.length,95);
elements.get('hmin').value='101';elements.get('hmin').onchange();
assert.equal(editor.getDocument().leds.length,95,'Invalid edit must preserve previous document');
assert(elements.get('message').textContent.includes('coordinate'),'Invalid edit must explain the problem');
console.log('PASS: shipped editor runs offline; guided frame, direction, furniture segment, validation, undo and invalid-edit recovery');
