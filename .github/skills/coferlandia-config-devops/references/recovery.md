# Go-around, failure, and recovery

Stop before another mutation when:

- project/environment identity is uncertain;
- returned state contradicts the contract;
- prepare includes unexpected fields or effects;
- plan is stale;
- validation fails;
- application is partial or activation is unsafe;
- required rollback information is unavailable for a consequential change.

Recovery order:

1. declare the last verified safe boundary;
2. stop additional writes/activation;
3. capture one consolidated diagnostic through the CLI;
4. determine whether native state changed;
5. use the CLI's rollback only when its preconditions remain valid and authority is explicit;
6. validate/health-check the restored or stabilized state;
7. return contract/adapter defects to Config Toolsmith rather than bypassing the facade.

Never hide partial execution or claim rollback from an unverified expected result.
