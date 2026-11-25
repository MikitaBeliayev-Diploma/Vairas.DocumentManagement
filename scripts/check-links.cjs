const fs=require('node:fs'),path=require('node:path');const root=path.resolve(__dirname,'..');let count=0;
for(const name of ['README.md','VALIDATION.md','CONTRACT-NOTES.md','documentation-update/README.md']){
 const file=path.join(root,name),source=fs.readFileSync(file,'utf8');
 for(const match of source.matchAll(/\[[^\]]+\]\((?:<([^>]+)>|([^\s)]+))\)/g)){
  const target=(match[1]||match[2]).split('#')[0];if(!target||/^[a-z][a-z0-9+.-]*:/i.test(target))continue;
  if(!fs.existsSync(path.resolve(path.dirname(file),decodeURIComponent(target))))throw Error(name+': missing link '+target);count++;
 }
}
console.log('Verified '+count+' local documentation links.');
