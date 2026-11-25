const fs=require('node:fs'),path=require('node:path'),{createRequire}=require('node:module');const root=path.resolve(__dirname,'..'),tool=createRequire(path.join(root,'scripts/contract-tools/package.json')),YAML=tool('yaml');
const stage=path.join(root,'documentation-update/tree');function write(name,value){const p=path.join(stage,name);fs.mkdirSync(path.dirname(p),{recursive:true});fs.writeFileSync(p,value);}
const api=JSON.parse(fs.readFileSync(path.join(root,'api/document-management.openapi.json'),'utf8'));
write('Communication/Api/document-management.openapi.yaml',YAML.stringify(api));
write('Communication/Async Api/document-management.asyncapi.yaml',fs.readFileSync(path.join(root,'api/document-management.asyncapi.yaml')));
for(const [service,file] of [['Vairas.LearningTracking','learning-tracking'],['Vairas.SchoolManagement','school-management'],['Vairas.UserManagement','user-management'],['Vairas.Authentication','authentication']]){
 const source=path.join(root,'..',service,'api',file+'.openapi.json');
 if(fs.existsSync(source))write('Communication/Api/'+file+'.openapi.yaml',YAML.stringify(JSON.parse(fs.readFileSync(source,'utf8'))));
}
write('Database/document-management/schema/implementation-schema.dbml',fs.readFileSync(path.join(root,'database/implementation.dbml')));
write('Database/document-management/docs/implementation.md',fs.readFileSync(path.join(root,'CONTRACT-NOTES.md')));
let reference='# Vairas Document Management implementation\n\nThe backend increment implements 28 HTTP operations. Generated contracts describe actual controller metadata; local test evidence and production deployment are separate.\n\n';
reference+='| Method | Route | Operation |\n|---|---|---|\n';for(const [route,p]of Object.entries(api.paths))for(const [m,o]of Object.entries(p))if(['get','post','put','patch','delete'].includes(m))reference+=`| ${m.toUpperCase()} | \`${route}\` | ${o.operationId} |\n`;
reference+='\nAll commands require a nonempty UUID Idempotency-Key. Where absent from the route, schoolId is a required query parameter. Exact retries reauthorize and return retained responses; GET resources supplies current state. Revisions, job status, historical evidence and current publication are independent.\n\nGeneration creates Prepared output. Ready publication requires review and a durable Learning decision. Files are private, scanned and immutable; download URLs require current user authentication and expire after 60 seconds. Current Ready bytes recheck exact evidence at use. Completion reports are internal manual-handoff artifacts; no external Regitra transmission or signature equivalence is asserted.\n';
write('Communication/reference/document-management.md',reference);
console.log('Staged documentation for review; the separate Documentation checkout was not modified.');
