const fs=require('node:fs'),path=require('node:path'),crypto=require('node:crypto'),{createRequire}=require('node:module');
const root=path.resolve(__dirname,'..'),tool=createRequire(path.join(root,'scripts/contract-tools/package.json'));
const {Parser}=tool('@asyncapi/parser'),Ajv=tool('ajv/dist/2020').default,formats=tool('ajv-formats'),{Parser:DbmlParser}=tool('@dbml/core');
const read=file=>fs.readFileSync(path.join(root,file),'utf8').replace(/^\uFEFF/,''); const check=(ok,m)=>{if(!ok)throw Error(m);};
(async()=>{
 for(const s of JSON.parse(read('source-manifest.json')).sources)check(crypto.createHash('sha256').update(fs.readFileSync(path.join(root,'source',path.basename(s.source)))).digest('hex')===s.sha256,'Frozen source changed: '+s.source);
 const parsed=await new Parser().parse(read('api/document-management.asyncapi.yaml')); const errors=parsed.diagnostics.filter(d=>d.severity===0);check(parsed.document&&errors.length===0,JSON.stringify(errors));
 const api=JSON.parse(read('api/document-management.openapi.json')),ajv=new Ajv({strict:false,allErrors:true});formats(ajv);ajv.addSchema({...api,$id:'urn:vairas:documents-openapi'});
 const bind=x=>Array.isArray(x)?x.map(bind):x&&typeof x==='object'?Object.fromEntries(Object.entries(x).map(([k,v])=>[k,k==='$ref'&&v.startsWith('#/')?'urn:vairas:documents-openapi'+v:bind(v)])):x;
 const examples=read('Vairas.DocumentManagement/Vairas.DocumentManagement.http'),vars=Object.fromEntries([...examples.matchAll(/^@(\w+)\s*=\s*(.*)$/gm)].map(m=>[m[1],m[2].trim()])),replace=s=>s.replace(/\{\{(\w+)\}\}/g,(_,k)=>vars[k]??k);
 let bodies=0,invalid=0;const covered=new Set();
 for(const block of examples.split(/^###/m)){
  const request=block.match(/^(GET|POST|PUT|PATCH|DELETE)\s+(\S+)/m);if(!request)continue;const method=request[1].toLowerCase(),url=new URL(replace(request[2]));
  const route=Object.keys(api.paths).find(p=>new RegExp('^'+p.replace(/[.*+?^$()|[\]\\]/g,'\\$&').replace(/\{[^}]+\}/g,'[^/]+')+'$').test(url.pathname)&&api.paths[p][method]);check(route,'Undocumented HTTP example');covered.add(method+' '+route);
  const captures=new RegExp('^'+route.replace(/[.*+?^$()|[\]\\]/g,'\\$&').replace(/\{[^}]+\}/g,'([^/]+)')+'$').exec(url.pathname);
  const pathNames=[...route.matchAll(/\{([^}]+)\}/g)].map(x=>x[1]);
  for(const parameter of api.paths[route][method].parameters||[]){
   if(!['path','query'].includes(parameter.in))continue;
   const value=parameter.in==='path'?decodeURIComponent(captures[pathNames.indexOf(parameter.name)+1]):url.searchParams.get(parameter.name);
   if(value===null){check(!parameter.required,'Missing required '+parameter.name);continue;}
   const validateParameter=ajv.compile(bind(parameter.schema));check(validateParameter(value),'Invalid '+parameter.name+': '+JSON.stringify(validateParameter.errors));
  }
  const body=block.match(/\r?\n\r?\n\s*(\{[\s\S]*\})\s*$/);if(!body)continue;const schema=api.paths[route][method].requestBody?.content?.['application/json']?.schema;check(schema,'Missing request schema');
  const validate=ajv.compile(bind(schema)),valid=validate(JSON.parse(replace(body[1]))),negative=/expected 400/i.test(block);check(negative?!valid:valid,request[1]+' '+route+': '+JSON.stringify(validate.errors));negative?invalid++:bodies++;
 }
 const operations=Object.entries(api.paths).flatMap(([p,v])=>Object.keys(v).filter(m=>['get','post','put','patch','delete'].includes(m)).map(m=>m+' '+p));check(operations.every(o=>covered.has(o)),'Missing HTTP examples');
 const database=new DbmlParser().parse(read('database/implementation.dbml'),'dbmlv2'),tables=database.schemas.reduce((n,s)=>n+s.tables.length,0);check(tables===17,'Expected 17 domain and operational tables');
 const result={sourceHashesVerified:7,asyncapiErrors:errors.length,httpOperations:operations.length,httpOperationsCovered:covered.size,positiveRequestBodies:bodies,negativeBodies:invalid,dbmlTables:tables};fs.mkdirSync(path.join(root,'evidence'),{recursive:true});fs.writeFileSync(path.join(root,'evidence/contracts-validation.json'),JSON.stringify(result,null,2));console.log(JSON.stringify(result));
})().catch(e=>{console.error(e.message);process.exitCode=1;});
