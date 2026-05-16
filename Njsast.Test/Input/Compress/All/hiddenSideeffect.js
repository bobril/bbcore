const obj = { b: false };
setB(obj);
if (obj.b) {
    console.log("Ok");
}
function setB(obj) {
    obj.b = true;
}
