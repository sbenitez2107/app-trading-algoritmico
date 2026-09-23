import { PlatformType as ServicePlatformType } from './trading-account.service';
import { PlatformType as ModelPlatformType } from '../models/platform-type.model';

describe('trading-account.service PlatformType re-export', () => {
  it('ReExportedPlatformType_IsReferenceIdenticalToTheModelEnum', () => {
    // Design D7: the alias becomes a one-line re-export, not a second, differently-shaped type.
    // Reference identity on the enum object itself is the strongest proof they are the same thing.
    expect(ServicePlatformType).toBe(ModelPlatformType);
    expect(ServicePlatformType.MT4).toBe(ModelPlatformType.MT4);
  });
});
