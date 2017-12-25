var u = require('uglify-js');

function print(t) {
	console.log(t); 
}

function ex(n, base) {
	print('/// '+n.documentation);
	if (base == null)
		print('interface IAst'+n.TYPE+' {');
	else
		print('interface IAst'+n.TYPE+' extends IAst'+base+' {');
	var i;
	for(i=0;i<n.SELF_PROPS.length;i++) {
		var prop = n.SELF_PROPS[i];
		var doc = n.propdoc[prop];
		var type = /\[(.+)\]/.exec(doc)[1];
		var afterScope = false;
		if (type.substr(type.length-2)==='/S') {
			type = type.substr(0,type.length-2);
			afterScope = true;
		}
		type = type.split('|').map(function(type){
			if (type.substr(type.length-1)==='?') {
				type = type.substr(0,type.length-1);
			}
			var isarray = false;
			if (type.substr(type.length-1)==='*') {
				isarray = true;
				type = type.substr(0,type.length-1);
			}
			if(type==='Object') {
				type='IDictionary<ISymbolDef>';
			} else if(type==='integer') {
				type='number';
			} else if (type === 'string' || type === 'number' || type==='boolean' || type==='RegExp') {
			} else if (type.substr(0,4)==='AST_') {
				type = 'IAst'+type.substr(4,type.length-4);
			} else {
				type= 'I'+type;
			}
			return type+(isarray?'[]':'');
		}).join('|');
		print('    /// '+doc.substr(doc.indexOf(']')+2)+(afterScope?' (After Scope)':''));
		print('    '+prop+'?: '+type+';');
	}
	print('}');
	print('');
	print('interface IAST_'+n.TYPE+' {');
	print('    new (props?: IAst'+n.TYPE+'): IAst'+n.TYPE+';');
	print('}');
	print('/// '+n.documentation);
	print('const AST_'+n.TYPE+': IAST_'+n.TYPE+';');
	print('');
	for(i=0;i<n.SUBCLASSES.length;i++) {
		ex(n.SUBCLASSES[i],n.TYPE);
	}
}

ex(u.AST_Node,null);
