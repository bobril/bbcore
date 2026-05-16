try {
    JSON.parse("{ bad json");
} catch (error: unknown) {
    console.error(error);
}
