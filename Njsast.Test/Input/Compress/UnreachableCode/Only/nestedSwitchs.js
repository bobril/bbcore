switch (a) {
    case 1: 
        switch (b) {
            case 2:
                call();
                break;
                callNever();
            default:
                call1();
                break;
                callNever();
        }
        call2();
        break;
    default:
        a++;
}