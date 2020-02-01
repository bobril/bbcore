!function(undefined) {
    "use strict";
    function loadImage() {
        return new Image();
    }
    new (function() {
        function Image_image2() {
            console.log("constructed");
        }
        return Image_image2;
    }())(), loadImage();
}();