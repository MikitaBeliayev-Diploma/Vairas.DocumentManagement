const fs=require('node:fs'),path=require('node:path'),{createRequire}=require('node:module');
const root=path.resolve(__dirname,'..'),tool=createRequire(path.join(root,'scripts/contract-tools/package.json')),YAML=tool('yaml');
const producer=path.join(root,'../Vairas.LearningTracking/api/learning-tracking.asyncapi.yaml');
const source=YAML.parse(fs.readFileSync(fs.existsSync(producer)?producer:path.join(root,'api/document-management.asyncapi.yaml'),'utf8'));
const eventNames=['PracticalRecordConfirmed','PracticalRecordRevised','TrainingEvidenceInvalidated','StudentEnrollmentStarted','StudentEnrollmentCompleted','StudentEnrollmentEnded'];
const document={asyncapi:'3.0.0',info:{title:'Vairas Document Management Integration Events',version:'1.0.0',description:'Versioned consumer contract. Broker transport is at-least-once; payloads are owner change hints. Physical producer-family topics and Subject filters are configured externally.'},defaultContentType:'application/json',channels:{},operations:{},components:source.components};
document.components.messages=Object.fromEntries(Object.entries(document.components.messages).filter(([n])=>eventNames.includes(n)));
for(const [name,c] of Object.entries(source.channels))if(Object.keys(c.messages).some(n=>eventNames.includes(n))){document.channels[name]=c;document.operations['consume'+Object.keys(c.messages)[0]]={action:'receive',channel:{$ref:'#/channels/'+name},messages:Object.keys(c.messages).map(n=>({$ref:'#/channels/'+name+'/messages/'+n}))};}
fs.mkdirSync(path.join(root,'api'),{recursive:true});fs.writeFileSync(path.join(root,'api/document-management.asyncapi.yaml'),YAML.stringify(document));
let dbml=fs.readFileSync(path.join(root,'source/schema.dbml'),'utf8').replace('Reviewed PROPOSED design.','Implementation projection: canonical domain baseline plus additive operational tables.');
const domain=path.join(root,'Vairas.DocumentManagement.Domain');
// One type per file: the entity declarations are spread over the Domain project's source files.
const entities=fs.readdirSync(domain).filter(f=>f.endsWith('.cs')).sort().map(f=>fs.readFileSync(path.join(domain,f),'utf8')).join('
');
const snake=s=>s.replace(/([a-z0-9])([A-Z])/g,'$1_$2').toLowerCase();
for(const m of entities.matchAll(/public sealed class (\w+) : ISchoolRow\s*\{([\s\S]*?)\n\}/g)){
 const [,name,body]=m;dbml+='\nTable '+snake(name)+' {\n';
 for(const c of body.matchAll(/public (Guid|long|int|string|bool|DateTimeOffset)(\?)? (\w+) \{ get; set; \}/g))dbml+='  '+snake(c[3])+' '+({Guid:'uuid',long:'bigint',int:'integer',string:'text',bool:'boolean',DateTimeOffset:'timestamptz'}[c[1]])+(c[3]==='Id'?' [pk, not null]':c[2]?'':' [not null]')+'\n';
 dbml+='}\n';
}
// operation_outcome is keyed by tenant + command; see SQL for conditional artifact uniqueness and deferred constraints.
dbml=dbml.replace(/Table operation_outcome \{([\s\S]*?)\n\}/,(_,b)=>'Table operation_outcome {'+b.replace('id uuid [pk, not null]','id uuid [not null]')+'\n indexes { (school_id,id) [pk] }\n}');
fs.mkdirSync(path.join(root,'database'),{recursive:true});fs.writeFileSync(path.join(root,'database/implementation.dbml'),dbml);
const api=JSON.parse(fs.readFileSync(path.join(root,'api/document-management.openapi.json'),'utf8'));
const id='11111111-1111-4111-8111-111111111111';
function sample(schema,name=''){
 if(schema.$ref)return sample(api.components.schemas[schema.$ref.split('/').pop()],name);
 if(schema.enum)return schema.enum[0];
 const type=Array.isArray(schema.type)?schema.type.find(x=>x!=='null'):schema.type;
 if(type==='object'||schema.properties)return Object.fromEntries(Object.entries(schema.properties||{}).filter(([n])=>!['replacesContractId'].includes(n)).map(([n,s])=>[n,sample(s,n)]));
 if(type==='array')return [];
 if(type==='integer'||type==='number')return 1;
 if(type==='boolean')return false;
 if(schema.format==='uuid')return id;
 if(schema.format==='date-time')return '2026-11-24T10:00:00Z';
 return ({kind:'PracticalJournal',name:'DEMO school template',reason:'Authorized administrator reviewed the exact version.',basis:'Externally signed PDF supplied by the school.',mode:'Ready',sourceSnapshotVersion:'evidence-'+id+'-1'})[name]||'synthetic-example';
}
let http='@baseUrl = https://localhost:7700\n@token = INSERT_CURRENT_USER_TOKEN\n@schoolId = '+id+'\n@operationId = 22222222-2222-4222-8222-222222222222\n@templatePath = ../.evidence/templates/PracticalJournal.docx\n\n# Replace synthetic IDs and revisions with owner/API responses. Use a new operationId for each distinct command; retain it for exact retries.\n';
for(const [route,p]of Object.entries(api.paths))for(const [method,o]of Object.entries(p)){
 if(!['get','post','put','patch','delete'].includes(method))continue;
 const target=route.replace(/\{([^}]+)\}/g,(_,name)=>name==='schoolId'?'{{schoolId}}':id);const params=(o.parameters||[]).filter(x=>x.in==='query');
 const query=params.filter(x=>x.required||x.name==='purpose').map(x=>x.name+'='+(x.name==='schoolId'?'{{schoolId}}':x.name==='purpose'?'Template':x.schema?.format==='uuid'?id:'synthetic-example')).join('&');
 http+='\n### '+o.operationId+'\n'+method.toUpperCase()+' {{baseUrl}}'+target+(query?'?'+query:'')+'\nAuthorization: Bearer {{token}}\nX-Correlation-ID: document-example-01\n';
 if(method!=='get')http+='Idempotency-Key: {{operationId}}\n';
 if(o.requestBody?.content?.['application/json'])http+='Content-Type: application/json\n\n'+JSON.stringify(sample(o.requestBody.content['application/json'].schema),null,2)+'\n';
 if(o.requestBody?.content?.['multipart/form-data'])http+='Content-Type: multipart/form-data; boundary=vairas-upload\n\n--vairas-upload\nContent-Disposition: form-data; name="file"; filename="demo.docx"\nContent-Type: application/vnd.openxmlformats-officedocument.wordprocessingml.document\n\n< {{templatePath}}\n--vairas-upload--\n';
}
http+='\n### Invalid generation body expected 400\nPOST {{baseUrl}}/api/v1/enrollments/'+id+'/journals?schoolId={{schoolId}}\nAuthorization: Bearer {{token}}\nIdempotency-Key: {{operationId}}\nContent-Type: application/json\n\n{}\n';
fs.writeFileSync(path.join(root,'Vairas.DocumentManagement/Vairas.DocumentManagement.http'),http);
console.log('Exported consumer AsyncAPI, implementation DBML and HTTP examples.');
