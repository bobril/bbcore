"use strict";
exports.svgPie = svgPie;

exports.svgCircle = svgCircle;

exports.svgRect = svgRect;

function polarToCartesian(centerX, centerY, radius, angleInDegrees) {
    var angleInRadians = angleInDegrees * Math.PI / 180;
    return {
        x: centerX + radius * Math.sin(angleInRadians),
        y: centerY - radius * Math.cos(angleInRadians)
    };
}

function svgDescribeArc(x, y, radius, startAngle, endAngle, startWithLine) {
    var absDeltaAngle = Math.abs(endAngle - startAngle);
    var close = false;
    if (absDeltaAngle > 360 - .01) {
        if (endAngle > startAngle) endAngle = startAngle - 359.9; else endAngle = startAngle + 359.9;
        if (radius === 0) return "";
        close = true;
    } else {
        if (radius === 0) {
            return [ startWithLine ? "L" : "M", x, y ].join(" ");
        }
    }
    var start = polarToCartesian(x, y, radius, endAngle);
    var end = polarToCartesian(x, y, radius, startAngle);
    var arcSweep = absDeltaAngle <= 180 ? "0" : "1";
    var largeArg = endAngle > startAngle ? "0" : "1";
    var d = [ startWithLine ? "L" : "M", start.x, start.y, "A", radius, radius, 0, arcSweep, largeArg, end.x, end.y ].join(" ");
    if (close) d += "Z";
    return d;
}

function svgPie(x, y, radiusBig, radiusSmall, startAngle, endAngle) {
    var p = svgDescribeArc(x, y, radiusBig, startAngle, endAngle, false);
    var nextWithLine = true;
    if (p[p.length - 1] === "Z") nextWithLine = false;
    if (radiusSmall === 0) {
        if (!nextWithLine) return p;
    }
    return p + svgDescribeArc(x, y, radiusSmall, endAngle, startAngle, nextWithLine) + "Z";
}

function svgCircle(x, y, radius) {
    return svgDescribeArc(x, y, radius, 0, 360, false);
}

function svgRect(x, y, width, height) {
    return "M" + x + " " + y + "h" + width + "v" + height + "h" + -width + "Z";
}

