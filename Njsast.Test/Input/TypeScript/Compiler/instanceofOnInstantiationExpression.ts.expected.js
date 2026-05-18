"use strict";
maybeBox instanceof Box; // OK
maybeBox instanceof (Box); // error
maybeBox instanceof (Box); // error
maybeBox instanceof ((Box)); // error
(Box) instanceof Object; // OK
(Box) instanceof Object; // OK
((Box)) instanceof Object; // OK
