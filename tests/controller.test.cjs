// Deterministic controller tests against a small DOM double, not browser/hardware tests.
const {test}=require('node:test');
const assert=require('node:assert/strict');
const vm=require('node:vm');
const {readFileSync}=require('node:fs');
const html=readFileSync(require('node:path').join(__dirname,'../index.html'),'utf8');
const script=html.match(/<script>([\s\S]*?)<\/script>/)[1];

function harness(){
  let now=1000,raf=null,interval=null,timerId=0;
  const timeouts=new Map(),downloads=[],draws=[];
  class Element{
    constructor(tag='div'){this.tagName=tag.toUpperCase();this.listeners={};this.children=[];this.style={};this.value='';this.textContent='';this.disabled=false;this.width=600;this.height=300;}
    addEventListener(name,fn){(this.listeners[name]??=[]).push(fn);}
    dispatch(name,props={}){const e={target:this,preventDefault(){},...props};for(const fn of this.listeners[name]||[])fn(e);}
    append(...children){this.children.push(...children);}
    replaceChildren(...children){this.children=children;}
    get firstElementChild(){return this.children[0];}
    get lastElementChild(){return this.children.at(-1);}
    querySelector(selector){for(const child of this.children){if(selector==='b'&&child.tagName==='B'||selector.startsWith('.')&&child.className===selector.slice(1))return child;const found=child.querySelector(selector);if(found)return found;}return null;}
    setAttribute(name,value){this[name]=value;}
    getBoundingClientRect(){return {width:600,height:300};}
    getContext(){return new Proxy({},{get:(_,method)=>(...args)=>draws.push([method,...args]),set:()=>true});}
    reportValidity(){return true;}
    focus(){}
    click(){this.dispatch('click');}
    remove(){}
  }
  const elements=Object.fromEntries([...html.matchAll(/id="([^"]+)"/g)].map(match=>[match[1],new Element()]));
  Object.assign(elements.surface,{value:'Test surface'});elements.mode.value='cursor';elements.connection.value='Wireless';elements.dpi.value='800';elements.duration.value='5';elements.settle.value='3';elements.zoom.value='auto';
  elements.trace.parentElement=new Element();elements.plotEmpty.append(new Element('strong'),new Element('span'));
  const document=new Element();document.documentElement=new Element();document.body=new Element();document.hidden=false;document.pointerLockElement=null;document.getElementById=id=>elements[id];document.createElement=tag=>new Element(tag);
  document.exitPointerLock=()=>{document.pointerLockElement=null;document.dispatch('pointerlockchange');};
  const window=new Element();window.devicePixelRatio=1;
  const context={document,window,performance:{now:()=>now},Date,Math,Number,String,Array,Object,JSON,Set,Blob,crypto:require('node:crypto').webcrypto,navigator:{userAgent:'Test DOM double'},devicePixelRatio:1,innerWidth:1200,innerHeight:900,
    requestAnimationFrame:fn=>{raf=fn;},setInterval:fn=>{interval=fn;},setTimeout:(fn,delay)=>{timeouts.set(++timerId,{fn,at:now+delay});return timerId;},clearTimeout:id=>timeouts.delete(id),ResizeObserver:class{observe(){}},confirm:()=>true,URL:{createObjectURL:blob=>{downloads.push(blob);return 'blob:test';},revokeObjectURL(){}},
  };
  vm.runInNewContext(script,context);
  function tick(value){now=value;for(const [id,timer] of [...timeouts])if(timer.at<=now){timeouts.delete(id);timer.fn();}interval?.();raf?.(now);}
  function move(x,y,{time=now,dx=0,dy=0,trusted=true}={}){now=time;document.dispatch('mousemove',{isTrusted:trusted,timeStamp:time,clientX:x,clientY:y,movementX:dx,movementY:dy});}
  function start(mode='cursor'){elements.mode.value=mode;elements.testForm.dispatch('submit');}
  async function exportRun(){elements.exportJSON.click();return JSON.parse(await downloads.at(-1).text()).runs.at(-1);}
  return {elements,document,window,start,tick,move,exportRun,draws,setNow:value=>{now=value;}};
}

test('countdown movement is excluded; tiny return movement is retained; timer saves exactly five seconds',async()=>{
  const h=harness();h.move(100,100);h.start();h.move(200,200,{time:3999});h.tick(4000);
  h.move(201,200,{time:4001});h.move(200,200,{time:4010});h.tick(9000);
  const run=await h.exportRun();assert.equal(run.samples.length,2);assert.deepEqual(run.samples[0],[1,1,0,1,0]);assert.equal(run.travel,2);assert.equal(run.x,0);assert.equal(run.elapsedMs,5000);assert.equal(run.completed,true);assert.equal(h.elements.start.disabled,false);assert.equal(h.elements.exportJSON.disabled,false);
});
test('an event that crosses the countdown boundary keeps the previous cursor position as origin',async()=>{
  const h=harness();h.move(100,100);h.start();h.move(101,100,{time:4001});h.tick(9000);
  const run=await h.exportRun();assert.deepEqual(run.capture.originClient,{x:100,y:100});assert.deepEqual(run.samples[0],[1,1,0,1,0]);
});
test('late first motion renders a valid activity bar after empty time buckets',async()=>{
  const h=harness();h.move(10,10);h.start();h.tick(4000);h.move(10,11,{time:8100});h.tick(8200);
  assert.equal(h.elements.timelinePeak.textContent,'Peak: 1 CSS px in one second');
  for(const call of h.draws)for(const arg of call.slice(1))if(typeof arg==='number')assert.ok(Number.isFinite(arg),`Non-finite ${call[0]} argument`);
  h.tick(9000);const run=await h.exportRun();assert.equal(run.samples.length,1);assert.equal(run.buckets[4].count,1);
});
test('untrusted events are ignored and motion exactly at the deadline is excluded',async()=>{
  const h=harness();h.move(10,10);h.start();h.tick(4000);h.move(50,50,{time:4500,trusted:false});h.move(11,10,{time:8999});h.move(100,100,{time:9000});h.tick(9010);
  const run=await h.exportRun();assert.equal(run.samples.length,1);assert.equal(run.travel,1);assert.equal(run.elapsedMs,5000);
});
test('losing focus saves a partial recording; later events do not change it',async()=>{
  const h=harness();h.move(10,10);h.start();h.tick(4000);h.move(11,10,{time:4500});h.setNow(5000);h.window.dispatch('blur');h.move(200,200,{time:6000});h.tick(6001);
  const run=await h.exportRun();assert.equal(run.elapsedMs,1000);assert.equal(run.completed,false);assert.equal(run.stopReason,'Window lost focus');assert.equal(run.samples.length,1);
});
test('Escape during settling cancels without creating a misleading zero-motion run',()=>{
  const h=harness();h.start();h.tick(2000);h.document.dispatch('keydown',{key:'Escape'});h.tick(2100);
  assert.equal(h.elements.exportJSON.disabled,true);assert.equal(h.elements.runCount.textContent,'0 runs');assert.equal(h.elements.statusTitle.textContent,'Countdown cancelled');
});
test('locked capture uses relative deltas rather than the stationary visible cursor',async()=>{
  const h=harness();h.elements.trace.requestPointerLock=()=>{h.document.pointerLockElement=h.elements.trace;h.document.dispatch('pointerlockchange');return Promise.resolve();};
  h.start('raw');h.tick(4000);h.move(100,100,{time:4100,dx:-1,dy:1});h.tick(9000);
  const run=await h.exportRun();assert.equal(run.unit,'browser units');assert.equal(run.mode,'raw');assert.equal(run.capture.unadjustedRequested,true);assert.deepEqual(run.samples[0],[100,-1,1,-1,1]);assert.equal(h.document.pointerLockElement,null);
});
test('a rejected lock produces an explicit error without recording or changing mode',async()=>{
  const h=harness();h.elements.trace.requestPointerLock=()=>Promise.reject(Object.assign(new Error(),{name:'NotSupportedError'}));h.start('raw');await new Promise(resolve=>setImmediate(resolve));h.tick(1100);
  assert.equal(h.elements.start.disabled,false);assert.equal(h.elements.exportJSON.disabled,true);assert.equal(h.elements.mode.value,'raw');assert.match(h.elements.message.textContent,/NotSupportedError/);
});
test('late pointer lock after cancelling is released instead of corrupting a new visible-cursor test',()=>{
  const h=harness();h.elements.trace.requestPointerLock=()=>undefined;h.start('locked');h.document.dispatch('keydown',{key:'Escape'});h.start('cursor');h.document.pointerLockElement=h.elements.trace;h.document.dispatch('pointerlockchange');assert.equal(h.document.pointerLockElement,null);assert.equal(h.elements.start.disabled,true);
});
