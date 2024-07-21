import ServerLayout from '@/components/server/ServerLayout';

import Desktop from './_layout/Desktop';
import Mobile from './_layout/Mobile';

const MainLayout = ServerLayout({ Desktop, Mobile });

MainLayout.displayName = 'Layout';

export default MainLayout;
