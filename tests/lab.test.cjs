const {test}=require('node:test');
const assert=require('node:assert/strict');
const {readFileSync}=require('node:fs');
const vm=require('node:vm');
const html=readFileSync(require('node:path').join(__dirname,'../index.html'),'utf8');
const script=html.match(/<script>([\s\S]*?)<\/script>/)[1];
new vm.Script(script); // Parse the entire application, including the UI.
const pure=script.slice(0,script.indexOf('\n(() => {'));
const Lab=vm.runInNewContext(pure+'\nLab;',{crypto:require('node:crypto').webcrypto});
const create=(extra={})=>Lab.makeRun({surface:'Cloth pad',dpi:800,polling:1000,connection:'Wireless',notes:'',mode:'cursor',duration:60,settle:5,...extra});

test('out-and-back jitter has travel and excursion even with zero net displacement',()=>{
  const run=create();Lab.addSample(run,100,1,0);Lab.addSample(run,150,-1,0);run.elapsedMs=60000;
  const s=Lab.summary(run);assert.equal(s.movement_events,2);assert.equal(s.total_travel,2);assert.equal(s.net_distance,0);assert.equal(s.peak_excursion,1);assert.equal(s.net_direction,'No displacement');assert.equal(s.events_per_second,2/60);assert.equal(s.first_movement_seconds,.1);
});
test('diagonal path, signs, direction sectors, and one-second activity buckets',()=>{
  const run=create();Lab.addSample(run,0,3,-4);Lab.addSample(run,1000,0,2);
  assert.equal(run.travel,7);assert.equal(run.peak,5);assert.equal(run.largest,5);assert.equal(run.x,3);assert.equal(run.y,-2);assert.equal(run.buckets[0].travel,5);assert.equal(run.buckets[1].travel,2);assert.equal(run.firstMs,0);assert.equal(Lab.direction(3,-4),'Up-right');assert.equal(Lab.direction(-1,1),'Down-left');
  assert.equal(run.minY,-4);assert.equal(run.maxX,3);assert.equal(run.directions[7],1);assert.equal(run.directions[2],1);
});
test('zero deltas, invalid values, countdown events, and events on/after deadline are excluded',()=>{
  const run=create();for(const [t,dx,dy] of [[0,0,0],[-1,1,1],[60000,1,1],[Infinity,1,1],[10,NaN,0]])assert.equal(Lab.addSample(run,t,dx,dy),false);
  assert.equal(run.samples.length,0);assert.equal(Lab.addSample(run,59999,.1,0),true);assert.equal(run.travel,.1);
});
test('stationary and partial runs retain idle time in event and travel rates',()=>{
  const run=create();run.elapsedMs=60000;let s=Lab.summary(run);assert.equal(s.events_per_second,0);assert.equal(s.first_movement_seconds,null);
  Lab.addSample(run,1000,1,0);run.elapsedMs=5000;run.stopReason='Window lost focus';s=Lab.summary(run);assert.equal(s.events_per_second,.2);assert.equal(s.recorded_seconds,5);assert.equal(s.completed,false);assert.equal(s.stop_reason,'Window lost focus');
});
test('sample cap keeps all retained samples without changing metrics after the cap',()=>{
  const run=create();for(let i=0;i<Lab.SAMPLE_LIMIT;i++)assert.equal(Lab.addSample(run,i/10,1,0),true);
  assert.equal(Lab.addSample(run,11000,1,0),false);assert.equal(run.travel,Lab.SAMPLE_LIMIT);assert.equal(run.samples.length,Lab.SAMPLE_LIMIT);
});
test('capture modes explicitly distinguish CSS pixels and unverified unadjusted browser deltas',()=>{
  assert.equal(create().unit,'CSS px');const raw=create({mode:'raw'});assert.equal(raw.unit,'browser units');assert.equal(raw.capture.unadjustedRequested,true);assert.equal(raw.capture.unadjustedVerified,false);assert.equal(create({mode:'locked'}).capture.unadjustedRequested,false);
});
test('CSV preserves decimals, signs, quotes, newlines and prevents spreadsheet formula injection',()=>{
  const run=create({surface:'=HYPERLINK("bad")',notes:'two, parts\n"quoted"'});Lab.addSample(run,12.25,-1,.5);run.elapsedMs=1000;
  const summary=Lab.summaryCSV([run]),events=Lab.eventsCSV(run);
  assert.ok(summary.includes('"\'=HYPERLINK(""bad"")"'));assert.ok(summary.includes('"two, parts\n""quoted"""'));assert.ok(events.includes('"12.25","-1","0.5","-1","0.5"'));assert.ok(events.startsWith('\uFEFF'));assert.ok(events.includes('"Up')===false);assert.ok(events.includes('"Down-left"'));
});
