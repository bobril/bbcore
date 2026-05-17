"use strict";
exports.count = count;

exports.only = only;

exports.toArray = toArray;

exports.map = map;

exports.forEach = forEach;

function count(children) {
    if (Array.isArray(children)) {
        let res = 0;
        for (let i = 0; i < children.length; i++) {
            res += count(children[i]);
        }
        return res;
    }
    if (children == undefined || children === false || children === true) return 0;
    return 1;
}

function only(children) {
    if (count(children) != 1) {
        throw new Error("Children.only() accepts only single child");
    }
    if (Array.isArray(children)) {
        for (let i = 0; i < children.length; i++) {
            let child = children[i];
            if (child == undefined || child === false || child === true) continue;
            if (Array.isArray(child)) {
                if (count(child) > 0) return only(child);
                continue;
            }
            return child;
        }
        if (children.length === 1) return only(children[0]);
        return null;
    }
    return children;
}

function toArray(children) {
    if (children == undefined || children === false || children === true) return [];
    if (Array.isArray(children)) {
        let res = [];
        for (let i = 0; i < children.length; i++) {
            res.push(...toArray(children[i]));
        }
        return res;
    }
    return [ children ];
}

function map(children, fn) {
    if (children == undefined || children === false || children === true) return [];
    if (Array.isArray(children)) {
        let res = [];
        for (let i = 0; i < children.length; i++) {
            mapRecursive(res, children[i], fn);
        }
        return res;
    }
    return [ fn(children, 0) ];
}

function mapRecursive(res, children, fn) {
    if (children == undefined || children === false || children === true) return;
    if (Array.isArray(children)) {
        for (let i = 0; i < children.length; i++) {
            mapRecursive(res, children[i], fn);
        }
        return;
    }
    res.push(fn(children, res.length));
}

function forEach(children, fn) {
    if (children == undefined || children === false || children === true) return;
    if (Array.isArray(children)) {
        let idx = 0;
        for (let i = 0; i < children.length; i++) {
            idx = forEachRecursive(children[i], fn, idx);
        }
        return;
    }
    fn(children, 0);
}

function forEachRecursive(children, fn, idx) {
    if (children == undefined || children === false || children === true) return idx;
    if (Array.isArray(children)) {
        for (let i = 0; i < children.length; i++) {
            idx = forEachRecursive(children[i], fn, idx);
        }
        return idx;
    }
    fn(children, idx);
    return idx + 1;
}

