if (notConst) {
    call();
} else if (false) {
    deadCode("the whole alternative branch should be eliminated");
}